using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static Assets.NetData;

namespace Assets
{
    static class NetHelper
    {
        public const int NumberOfTimeSyncChecks = 16;
        public const int ServerPort = 7777;
        public const int DatagramSize = 8192;
        public const int MaxConnectionAttempts = 10;
        public const int TimeSyncAttempts = 3;
        public const int ConnectTimeout = 1500;
        public const int TimeSyncTimeout = 500;
        public const int FakeMinLag = 0;//100;
        public const int FakeMaxLag = 0;//101;
        public const int FakeDropPercent = 0;//8;
        public const int DisconnectTimer = 300; // s
        public const int MaxRewindTime = 1000;
        public const int InterpolationTime = 1000;
        public const int MaxInterpolationStates = InterpolationTime * UpdateRate / 1000 + 5;

        public const int UpdateRate = 100;
        public const int UpdateDelay = (int)(1000 / UpdateRate + 0.5);
        public const int SendRate = 1;
        public const int SendDelay = (int)(1000 / SendRate + 0.5);

        static System.Random r = new System.Random();
        static ConcurrentQueue<LagNetData> lagQueue = new ConcurrentQueue<LagNetData>();
        static Thread lagThread;
        static Mutex lagThreadMutex = new Mutex();


        class LagNetData
        {
            private byte[] byteData;
            public NetData Data { get; set; }
            private UdpClient client;
            private IPEndPoint endPoint;
            public bool isServer;

            public LagNetData(NetData data, UdpClient client, IPEndPoint endPoint)
            {
                byteData = data.ToBytes();
                Data = data;
                this.client = client;
                this.endPoint = endPoint;
                if (endPoint.Port != 7777)
                    isServer = true;
            }

            public void Send()
            {
                client.Send(byteData, byteData.Length, endPoint);
            }
        }



        public static void SetUdpConnReset(UdpClient client)
        {
            const uint IOC_IN = 0x80000000;
            const uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            client.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
        }

        public static byte[] ConstructConnectionMessage()
        {
            return new byte[] { NumberOfTimeSyncChecks + (byte)MessageType.Connect };
        }

        public static bool BitIsSet(this byte b, byte index)
        {
            return ((b >> index) & 1) != 0;
        }

        public static byte SetBit(this byte b, byte index, bool set)
        {
            if (set)
            {
                return (byte)((1 << index) | b);
            }
            return (byte)(~(1 << index) & b);
        }

        public static void SendNetData(NetData data, UdpClient client, IPEndPoint endPoint)
        {
            data.TimeStamp = NetData.GetTimeStamp();

            if (FakeDropPercent > 0 && r.Next(100) < FakeDropPercent)
                return;

            if (FakeMaxLag > FakeMinLag)
            {
                lagQueue.Enqueue(new LagNetData(data, client, endPoint));
                if (lagThreadMutex.WaitOne(1))
                {
                    if (lagThread?.IsAlive != true || lagThread?.ThreadState != ThreadState.Running)
                    {
                        lagThread = new Thread(new ThreadStart(LagSend));
                        lagThread.IsBackground = true;
                        lagThread.Start();
                    }
                    lagThreadMutex.ReleaseMutex();
                }
            }
            else
            {
                byte[] byteData = data.ToBytes();
                client.Send(byteData, byteData.Length, endPoint);
            }
        }

        private static void LagSend()
        {
            int randomSleep;
            while (lagQueue.TryDequeue(out LagNetData data))
            {
                randomSleep = r.Next(FakeMinLag, FakeMaxLag);
                long preSleep = data.Data.TimeStamp;
                if (data.Data.TimeStamp + randomSleep * 1000 > NetData.GetTimeStamp())
                {
                    Thread.Sleep((int)((data.Data.TimeStamp - NetData.GetTimeStamp()) / 1000 + randomSleep));
                }
                //if (data.isServer)
                //    Debug.SetPinMessage($"Pre: {preSleep} Post: {NetData.GetTimeStamp()} Diff: {NetData.GetTimeStamp() - preSleep}");
                data.Send();
            }
        }

        public static NetData AwaitData(UdpClient client, ref IPEndPoint endPoint, bool isServer = false)
        {
            byte[] receivedData;
            NetData result;
            try
            {
                receivedData = client.Receive(ref endPoint);

                if (receivedData.Length == 0)
                    result = new NetData($"Received empty data from: {endPoint}");
                else
                {
                    NetData data = NetData.ParseData(receivedData);
                    if (data.MsgType != MessageType.Data)
                        Debug.Log($"{endPoint} sent - {data?.ToString()}");
                    else if (!isServer)
                    {
                        //if (data.RawData.Array == null)
                        //    Debug.Log($"L: {client.Client.LocalEndPoint} S: {endPoint} M: ");
                        //else
                        //    Debug.Log($"L: {client.Client.LocalEndPoint} S: {endPoint} M: {Encoding.ASCII.GetString(data.RawData.Array)}");
                    }
                    return data;
                }
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    case SocketError.Interrupted:
                        result = new NetData("Connection was closed: " + e.Message);
                        break;
                    case SocketError.ConnectionReset:
                        result = new NetData("The connection was reset by the remote peer.");
                        break;
                    case SocketError.TimedOut:
                        result = new NetData("Timeout waiting for connection.");
                        break;
                    default:
                        result = new NetData($"Error {e.ErrorCode} occurred: {e.Message}");
                        break;
                }
            }

            return result;
        }

        public static byte[] Decrypt(byte[] data)
        {
            // Decrypt data
            return data;
        }

        public static byte[] Encrypt(byte[] data)
        {
            // Encrypt data
            return data;
        }

    }
}