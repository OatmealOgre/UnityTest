using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Net = Assets.NetHelper;
using Assets;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;

public class Server
{

    public GameState CurrentState { get; set; }

    public ConcurrentQueue<NetData> receivedData = new ConcurrentQueue<NetData>();
    public ConcurrentDictionary<IPEndPoint, NetConnection> connections = new ConcurrentDictionary<IPEndPoint, NetConnection>();
    public IPEndPoint[] connectionKeys;

    int MaxConnections = 4;

    IPEndPoint localEndpoint;
    IPEndPoint recvEndPoint;
    IPEndPoint anyEndPoint;
    public UdpClient client;

    int port = Net.ServerPort;
    Thread thread;
    System.Random r = new System.Random();

    byte serverTick = 0;

    TimeSpan lastFrameTime = new TimeSpan(0);

    // Start is called before the first frame update
    public void Start()
    {
        CurrentState = new GameState();

        anyEndPoint = new IPEndPoint(IPAddress.Any, port);
        recvEndPoint = anyEndPoint;
        client = new UdpClient(recvEndPoint);
        Net.SetUdpConnReset(client);

        thread = new Thread(new ThreadStart(NetworkLoop));
        thread.IsBackground = true;
        thread.Start();
    }

    /// <summary>
    /// The loop handling incoming data to the server.
    /// </summary>
    private void NetworkLoop()
    {
        while (true)
        {
            HandleData(AwaitMessage());
        }
    }

    private void HandleData(NetData data)
    {
        if (data.MsgType == NetData.MessageType.Invalid)
            Debug.LogError(data.ErrorMsg);

        if (data.MsgType == NetData.MessageType.Connect)
        {
            HandleConnectionMsg(data);
        }
        else if (connections.TryGetValue(recvEndPoint, out NetConnection connection))
        {
            if (data.MsgType == NetData.MessageType.Data)
            {
                //HandleGameData(data, connection);
            }
            else if (data.MsgType == NetData.MessageType.FinishedLoading)
            {
                connection.CurrentState = NetConnection.State.Ready;
            }

            if (data.MsgType == NetData.MessageType.Invalid)
            {
                Disconnect(recvEndPoint, false);
            }
            else
            {
                connection.LastMsg = data;
                connection.LastMsgTime = NetData.watch.Elapsed;
            }
        }
    }

    private void HandleGameData(NetData data, NetConnection connection)
    {
        if (data.Tick != connection.LastMsg.Tick)
        {
            int index = connection.AckData(data.AckId);
            //if (index >= 0)
            //    Debug.SetPinMessage($"Rtt: {(NetData.GetTimeStamp() - connection.SentData[index].TimeStamp).ToString("D6")} - Average Rtt: {connection.GetAverageRtt().ToString("0000.00")}");
        }

        if (data.Commands != null && data.Commands.Length > 0)
        {
            for (int i = 0; i < data.Commands.Length; i++)
            {
                if (connection.LastCommandId < data.Commands[i].Id || connection.LastCommandId > ushort.MaxValue / 2 && data.Commands[i].Id > 0)
                {
                    data.Commands[i].Perform(CurrentState, connection.Id);
                    connection.LastCommandId = data.Commands[i].Id;
                }
            }
        }
    }

    private void Disconnect(IPEndPoint endPoint, bool sendDisconnectMessage = true)
    {
        if (connections.TryRemove(endPoint, out NetConnection connection))
        {
            Debug.Log($"Disconnecting: {endPoint}");
            CurrentState.RemovePlayer(connection.Id);
            connectionKeys = connections.Keys.ToArray();
            if (sendDisconnectMessage)
                SendNetData(new NetData(NetData.MessageType.Disconnect), endPoint);
        }
    }

    private void HandleConnectionMsg(NetData data)
    {
        if (!connections.TryGetValue(recvEndPoint, out NetConnection connection))
        {
            if (connections.Count < MaxConnections)
            {
                connection = new NetConnection();
                connections[recvEndPoint] = connection;
                CurrentState.AddPlayer(connection.Id);
                connectionKeys = connections.Keys.ToArray();
                //Debug.AddPoint(connections[recvEndPoint].Id, 0, 0);
            }
            else
                return;
        }
        NetData response = NetData.ConnectMessage;
        byte[] connectionIdBytes = BitConverter.GetBytes(connection.Id);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(connectionIdBytes);
        response.RawData = new ArraySegment<byte>(connectionIdBytes);
        SendNetData(response);
    }

    private void SendNetData(NetData data, IPEndPoint endPoint)
    {
        if (data.MsgType != NetData.MessageType.Data)
        {
            Net.SendNetData(data, client, endPoint);
        }
        else if (connections.TryGetValue(endPoint, out NetConnection connection))
        {
            data.AckId = connection.LastCommandId;
            connection.AddSentData(data);
            Net.SendNetData(data, client, endPoint);
        }
    }

    private void SendNetData(NetData data)
    {
        SendNetData(data, recvEndPoint);
    }

    private NetData AwaitMessage()
    {
        recvEndPoint = anyEndPoint;
        return Net.AwaitData(client, ref recvEndPoint, true);
    }

    public void ServerUpdate()
    {
        TimeSpan startTime = NetData.watch.Elapsed;
        serverTick = (byte)((serverTick + 1) % (byte.MaxValue + 1));

        if (CurrentState != null)
            CurrentState.ToWorld();
        //Debug.PointsFromState(CurrentState);
        //Debug.Update();
        if (connectionKeys != null)
        {
            NetConnection connection;
            for(int i = 0; i < connectionKeys.Length; i++)
            {
                if (connections.TryGetValue(connectionKeys[i], out connection) && connection.CurrentState == NetConnection.State.Ready)
                    HandleGameData(connection.LastMsg, connection);
            }

            GameState state = CurrentState?.Clone();
            for (int i = 0; i < connectionKeys.Length; i++)
            {
                if (connections.TryGetValue(connectionKeys[i], out connection) && connection.CurrentState == NetConnection.State.Ready)
                {
                    if ((NetData.watch.Elapsed - connection.LastMsgTime).TotalSeconds >= Net.DisconnectTimer)
                    {
                        Debug.Log($"Lost contact to {connectionKeys[i]}");
                        Disconnect(connectionKeys[i]);
                    }
                    else
                    {
                        long timeDiff = NetData.GetTimeStamp() - connection.LastSendAdjustedTimeStamp;
                        if (timeDiff > Net.SendDelay * 2000)
                        {
                            timeDiff = Net.SendDelay * 1000;
                        }
                        if (timeDiff >= Net.SendDelay * 1000)
                        {
                            SendGameState(connectionKeys[i], state);
                            connection.LastSendAdjustedTimeStamp = NetData.GetTimeStamp() + Net.SendDelay * 1000 - timeDiff;
                        }
                    }
                }
            }

        }

        //lastFrameTime = NetData.watch.Elapsed - startTime;
        //if (lastFrameTime.TotalMilliseconds + 1 < Net.UpdateDelay)
        //{
        //    Thread.Sleep((int)(Net.UpdateDelay - lastFrameTime.TotalMilliseconds - 1));
        //    lastFrameTime = NetData.watch.Elapsed - startTime;
        //}

    }

    private void SendGameState(IPEndPoint endPoint, GameState state)
    {
        SendNetData(new NetData(NetData.MessageType.Data) { GameState = state, Tick = serverTick }, endPoint);
    }
}
