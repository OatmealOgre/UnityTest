using System;
using Net = Assets.NetHelper;
using static Assets.NetHelper;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.IO;
using UnityEngine;

namespace Assets
{
    public class NetData
    {
        private static readonly ArraySegment<byte> EmptyData = new ArraySegment<byte>();
        static readonly int MessageTypeCount = Enum.GetValues(typeof(MessageType)).Length;

        static public Stopwatch watch;
        public static readonly NetData ConnectMessage;
        public readonly string ErrorMsg;

        public enum MessageType
        {
            Connect = 0,
            Disconnect = 1,
            Data = 2,
            Invalid = 3,
            Denied = 4,
            Load = 5,
            FinishedLoading = 6,
        }
        public readonly MessageType MsgType;
        public byte Tick { get; set; }
        public ushort AckId { get; set; }

        public long TimeStamp { get; set; }
        public ArraySegment<byte> RawData { get; set; }
        public GameState GameState { get; set; }

        public ClientCommand[] Commands { get; set; }

        static NetData()
        {
            watch = new Stopwatch();
            watch.Start();
            ConnectMessage = new NetData(MessageType.Connect) { TimeStamp = 0 };
        }

        public NetData()
        {
            TimeStamp = GetTimeStamp();
        }

        public NetData(string errorMessage)
        {
            MsgType = MessageType.Invalid;
            ErrorMsg = errorMessage;
            TimeStamp = -1;
        }

        public NetData(byte msgTypeId) : this()
        {
            MsgType = GetMessageType(msgTypeId);
        }

        public NetData(MessageType messageType) : this()
        {
            MsgType = messageType;
        }

        /// <summary>
        /// Gets time since start in ticks / 10. (Milliseconds * 1000)
        /// </summary>
        /// <returns>Milliseconds * 1000</returns>
        public static long GetTimeStamp()
        {
            //return DateTime.Now.Ticks / 10;
            return (long)(watch?.Elapsed.TotalMilliseconds * 1000);
        }

        public static MessageType GetMessageType(byte id)
        {
            if (id < MessageTypeCount)
            {
                return (MessageType)id;
            }
            return MessageType.Invalid;
        }

        public static NetData ParseData(byte[] data)
        {
            MemoryStream stream = new MemoryStream(data);
            NetData result;
            using (BinaryReader reader = new BinaryReader(stream))
            {

                result = new NetData(reader.ReadByte());
                if (stream.Length > stream.Position)
                    result.Tick = reader.ReadByte();
                if (stream.Length > stream.Position)
                    result.AckId = reader.ReadUInt16();
                if (stream.Length > stream.Position)
                    result.TimeStamp = reader.ReadInt64();
                if (result.MsgType == MessageType.Data)
                {
                    if (stream.Length > stream.Position)
                        result.GameState = GameState.ReadState(reader);
                    if (stream.Length > stream.Position)
                        result.Commands = ClientCommand.ReadCommands(reader);
                }

                if (stream.Length > stream.Position)
                {
                    result.RawData = new ArraySegment<byte>(reader.ReadBytes((int)(stream.Length - stream.Position)));
                }
            }
            return result;
        }

        public byte[] ToBytes()
        {
            MemoryStream stream = new MemoryStream();

            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((byte)MsgType);
                writer.Write(Tick);
                writer.Write(AckId);
                writer.Write(TimeStamp);
                if (MsgType == MessageType.Data)
                {
                    if (GameState != null)
                    {
                        GameState.WriteState(writer);
                    }
                    else
                    {
                        writer.Write((byte)0);
                    }
                    ClientCommand.Write(Commands, writer);
                }

                if (RawData.Count > 0)
                    writer.Write(RawData.Array);

            }
            return Net.Encrypt(stream.ToArray());
        }

        public override string ToString()
        {
            return $"Type: {MsgType}, Tick: {Tick}, TimeStamp: {TimeStamp}";
        }
    }
}