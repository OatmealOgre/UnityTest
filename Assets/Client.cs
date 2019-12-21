using Net = Assets.NetHelper;
using Assets;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Threading;
using System;
using System.Net;
using System.Text;
using Diagnostics = System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Assets
{
    public class Client
    {
        //for debugging:
        private int lastN = 0;


        private ConcurrentQueue<NetData> receivedData;
        public ConcurrentQueue<NetData> sendData;
        public UdpClient client;
        private Thread thread;
        private bool connected;
        private GameState[] gameStates;
        private long[] timeStamps;
        public GameState displayState;
        private NetData lastData;
        private byte clientTick;

        private Mutex stateMutex;

        private ClientCommand[] commands;
        public int unAckedCommands;
        public const int MaxCommands = 100;

        public ushort Id { get; set; }

        public double MinRtt { get; private set; }
        public long TimeOffset { get; private set; }

        private IPEndPoint serverEndPoint;
        System.Random r = new System.Random();

        public Client()
        {
            receivedData = new ConcurrentQueue<NetData>();
            sendData = new ConcurrentQueue<NetData>();
            client = new UdpClient(0);
            Net.SetUdpConnReset(client);

            connected = false;
            gameStates = new GameState[Net.MaxInterpolationStates];
            timeStamps = new long[gameStates.Length];
            for (int i = 0; i < gameStates.Length; i++)
            {
                gameStates[i] = new GameState();
                timeStamps[i] = 0;
            }
            displayState = new GameState();
            commands = new ClientCommand[MaxCommands];
            stateMutex = new Mutex();
        }

        private void AddCommand(ClientCommand command)
        {
            if (unAckedCommands < MaxCommands)
            {
                commands[unAckedCommands++] = command;
            }
            else
            {
                for (int i = 0; i < MaxCommands - 1; i++)
                {
                    commands[i] = commands[i + 1];
                }
                commands[commands.Length - 1] = command;
            }
        }

        public void Connect(string ip, int port = Net.ServerPort)
        {
            if (thread == null)
            {
                thread = new Thread(new ThreadStart(NetworkLoop));
                thread.IsBackground = true;
            }
            else if (thread.ThreadState == ThreadState.Running)
            {
                throw new Exception("Multiple connection calls attempted!");
            }
            serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            thread.Start();
        }

        public void HandleKeyPress(ConsoleKey key, float deltaTime)
        {

            if (connected)
            {
                ClientCommand command = new ClientCommand();
                float x = Input.GetAxis("Horizontal");
                float y = Input.GetAxis("Vertical");

                if (displayState.GetPlayer(Id) != null)
                {
                    command.Left = x < -0.01;
                    command.Right = x > 0.01;
                    command.Up = y < -0.01;
                    command.Down = y > 0.01;
                }


                if (deltaTime > Net.MaxRewindTime / 1000)
                    deltaTime = Net.MaxRewindTime / 1000;

                command.Duration = deltaTime;

                if (command.Left || command.Right || command.Up || command.Down)
                    AddCommand(command);
                //switch (key)
                //{
                //    case ConsoleKey.LeftArrow:
                //        AddCommand(new ClientCommand { Left = true, Duration = deltaTime });
                //        break;
                //    case ConsoleKey.UpArrow:
                //        AddCommand(new ClientCommand { Up = true, Duration = deltaTime });
                //        break;
                //    case ConsoleKey.RightArrow:
                //        AddCommand(new ClientCommand { Right = true, Duration = deltaTime });
                //        break;
                //    case ConsoleKey.DownArrow:
                //        AddCommand(new ClientCommand { Down = true, Duration = deltaTime });
                //        break;
                //    default:
                //        break;
                //}
            }
        }

        public void Update()
        {
            if (!RunCommands())
            {
                Debug.LogError("Run failed");
            }
            if (displayState != null)
                displayState.ToWorld();
            //Debug.PointsFromState(displayState);
            //Debug.Update();
        }

        private void NetworkLoop()
        {
            if (!EstablishConnection())
            {
                return;
            }
            Debug.Log($"Connection made to: {serverEndPoint}");

            NetData data = null;
            for (int i = 0; i < Net.MaxConnectionAttempts; i++)
            {
                SendNetData(new NetData(NetData.MessageType.FinishedLoading));
                data = AwaitResponse();
                if (data.MsgType == NetData.MessageType.Data)
                {
                    connected = true;
                    break;
                }
                else
                {
                    Debug.Log($"Received {data}");
                }
            }

            client.Client.ReceiveTimeout = 0;
            while (connected)
            {
                clientTick++;

                if (data.MsgType == NetData.MessageType.Data)
                {
                    HandleReceivedState(data);
                    //RunCommands();
                    //Debug.SetPinMessage($"Unacked: {unAckedCommands}");

                    NetData sendD;
                    bool hadDataToSend = false;
                    while (sendData.TryDequeue(out sendD))
                    {
                        SendNetData(sendD);
                        hadDataToSend = true;
                    }
                    if (!hadDataToSend)
                    {
                        SendNetData(new NetData(NetData.MessageType.Data));
                    }
                }

                data = AwaitResponse();
                lastData = data;
                if (data.MsgType == NetData.MessageType.Disconnect)
                {
                    connected = false;
                }
            }
        }

        private void HandleReceivedState(NetData data)
        {
            stateMutex.WaitOne();
            for (int i = 0; i < gameStates.Length - 1; i++)
            {
                gameStates[i] = gameStates[i + 1];
                timeStamps[i] = timeStamps[i + 1];
            }
            gameStates[gameStates.Length - 1] = data.GameState.Clone();
            timeStamps[timeStamps.Length - 1] = NetData.GetTimeStamp();

            AckCommands(data.AckId);
            stateMutex.ReleaseMutex();
        }

        private GameState InterPolateState(ushort id, GameState oldState, GameState toState, double fraction)
        {
            GameState result = oldState.Clone();
            GameState.Player match;
            for (int i = 0; i < (int)toState.Players?.Length; i++)
            {
                match = result.GetPlayer(toState.Players[i].id);
                if (match == null)
                    match = result.AddPlayer(toState.Players[i].id);

                if (match.id == id)
                {
                    match.x = gameStates[gameStates.Length - 1].Players[i].x;
                    match.y = gameStates[gameStates.Length - 1].Players[i].y;
                }
                else
                {
                    
                    match.x += (float)((toState.Players[i].x - match.x) * fraction);
                    match.y += (float)((toState.Players[i].y - match.y) * fraction);
                }
            }
            return result;
        }

        private bool RunCommands()
        {
            if (stateMutex.WaitOne(1))
            {
                //double timeAgo = 0;
                //int n = 1;
                //long timeStamp = NetData.GetTimeStamp();
                //while (timeAgo < Net.InterpolationTime * 1000 && n < timeStamps.Length)
                //{
                //    timeAgo = timeStamp - timeStamps[timeStamps.Length - ++n];
                //}
                //timeAgo -= Net.InterpolationTime * 1000;

                //if (timeAgo <= 0)
                //    timeAgo = 0;

                //double diff = timeStamps[timeStamps.Length - (n - 1)] - timeStamps[timeStamps.Length - n];

                //if (diff > 0)
                //{
                //    double frac = timeAgo / diff;
                //    if (frac > 1)
                //    {
                //        frac = 1;
                //    }
                //    displayState = InterPolateState(Id, gameStates[timeStamps.Length - n], gameStates[timeStamps.Length - (n - 1)], frac);

                //    for (int i = 0; i < unAckedCommands; i++)
                //    {
                //        ClientCommand com = commands[i];
                //        if (commands[i] != null)
                //            commands[i].Perform(displayState, Id);
                //    }
                //}

                double targetTimeStamp = NetData.GetTimeStamp() - Net.InterpolationTime * 1000;
                if (targetTimeStamp < 0)
                    targetTimeStamp = 0;

                int n;
                for (n = 1; n < timeStamps.Length && targetTimeStamp > timeStamps[n]; n++)
                {
                }
                n--;

                if (n >= timeStamps.Length - 1)
                {
                    // If used, insert extrapolation here
                    displayState = gameStates[gameStates.Length - 1].Clone();
                    Debug.Log("Missing stamps or issue with target timestamp");
                }
                else if (timeStamps[n] >= 0 && timeStamps[n] < timeStamps[n + 1] && timeStamps[n] < targetTimeStamp)
                {
                    // Calculate fraction of time that the target time is between the two states timestamps
                    double frac = (targetTimeStamp - timeStamps[n]) / (timeStamps[n + 1] - timeStamps[n]);
                    displayState = InterPolateState(Id, gameStates[n], gameStates[n + 1], frac);
                }
                else
                {
                    //true false false
                    //Debug.Log($"First time or possible error, add some code here?  >= 0 : {timeStamps[n] >= 0 }  n.t < n+. t : { timeStamps[n] < timeStamps[n + 1] } n.t < tt { timeStamps[n] < targetTimeStamp} ");
                    // 0 0 0
                    Debug.Log($"n.t: {timeStamps[n]} n+.t: {timeStamps[n + 1]} t.t: {targetTimeStamp}");
                }

                for (int i = 0; i < unAckedCommands; i++)
                {
                    if (commands[i] != null)
                        commands[i].Perform(displayState, Id);
                }

                stateMutex.ReleaseMutex();
                return true;
            }
            return false;
        }

        private void AckCommands(ushort ackId)
        {
            //TODO make some error detection and correction
            //ClientCommand lastAcked = null;
            //for (int i = 0; i < unAckedCommands; i++)
            //{
            //    if (commands[i].Id == ackId)
            //    {
            //        lastAcked = commands[i];
            //        break;
            //    }
            //}
            //if (lastAcked == null)
            //    return;

            //int index = -1;
            //for (int i = 0; i < unAckedCommands; i++)
            //{
            //    if (index >= 0)
            //    {
            //        commands[i - index - 1] = commands[i];
            //        commands[i] = null;
            //    }
            //    else if (commands[i] == lastAcked)
            //    {
            //        index = i;
            //    }
            //}
            //if (index >= 0)
            //{
            //    unAckedCommands -= index + 1;
            //}
            int index = -1;
            for (int i = 0; i < unAckedCommands; i++)
            {
                if (commands[i] == null)
                {
                    unAckedCommands = i;
                    Debug.LogError($"Command[{i}] unexpectedly null!");
                }
                else if (commands[i].Id == ackId)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
                return;
            for (int i = 0; i < unAckedCommands - index - 1; i++)
            {
                commands[i] = commands[index + i + 1];
                commands[index + i + 1] = null;
            }
            
            unAckedCommands -= index + 1;
        }

        public void AddSendData(NetData data)
        {
            sendData.Enqueue(data);
        }

        private bool EstablishConnection()
        {
            int attempts = 0;
            NetData response;
            client.Client.ReceiveTimeout = Net.ConnectTimeout;
            Diagnostics.Stopwatch watch = new Diagnostics.Stopwatch();

            do
            {
                watch.Restart();
                NetData sendData = NetData.ConnectMessage;
                SendNetData(sendData);
                response = AwaitResponse();
                watch.Stop();
                if (response != null && response.MsgType != NetData.MessageType.Invalid)
                    break;
                if (watch.ElapsedMilliseconds < Net.ConnectTimeout)
                {
                    Thread.Sleep((int)(Net.ConnectTimeout - watch.ElapsedMilliseconds));
                    Debug.LogError("Retrying...");
                }
            } while (++attempts < Net.MaxConnectionAttempts);

            if (response != null && response.MsgType == NetData.MessageType.Connect)
            {
                if (!BitConverter.IsLittleEndian)
                {
                    byte[] reverse = response.RawData.Array;
                    Array.Reverse(reverse);
                    response.RawData = new ArraySegment<byte>(reverse);
                }
                Id = BitConverter.ToUInt16(response.RawData.Array, 0);
                Debug.Log($"Id is: {Id}");
                return true;
            }
            else
            {
                if (attempts == 0)
                {
                    Debug.LogError($"Data received: {response}");
                }
                Debug.LogError($"Connection attempt failed after {attempts} tries");
                return false;
            }
        }

        private void SendNetData(NetData data)
        {
            if (lastData != null)
                data.AckId = lastData.Tick;
            data.Tick = clientTick;

            if (data.MsgType == NetData.MessageType.Data && unAckedCommands > 0)
            {
                data.Commands = commands.Where(c => c != null).ToArray();
            }
            Net.SendNetData(data, client, serverEndPoint);
        }

        private NetData AwaitResponse()
        {
            NetData result = Net.AwaitData(client, ref serverEndPoint);
            if (result.MsgType == NetData.MessageType.Invalid)
                Debug.LogError(result.ErrorMsg);
            return result;
        }

        public bool GetNextData(out NetData data)
        {
            return receivedData.TryDequeue(out data);
        }
    }
}