using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Assets
{
    public class NetConnection
    {
        const int NumberOfRememberedStates = (int)((NetHelper.MaxRewindTime / NetHelper.UpdateDelay) + 0.5);

        private static ushort IdCounter = 0;

        //public IPEndPoint EndPoint { get; set; }

        public TimeSpan LastMsgTime { get; set; }

        public NetData LastMsg { get; set; }

        public ushort Id { get; set; }

        public State CurrentState { get; set; }

        public BitArray Acks { get; set; }

        public NetData[] SentData { get; set; }

        public int[] Rtts { get; set; }
        private int rttNumber;

        private int numberOfStates;

        public long LastSendAdjustedTimeStamp { get; set; } // for limiting send amount

        public ushort LastCommandId { get; set; }

        public enum State
        {
            Connected,
            TimeSync,
            Loading,
            Ready,
            Disconnected,
        }

        public NetConnection()
        {
            Id = IdCounter++;
            CurrentState = State.Connected;
            Acks = new BitArray(NumberOfRememberedStates);
            SentData = new NetData[NumberOfRememberedStates];
            Rtts = new int[100];
            numberOfStates = 0;
        }

        public void AddSentData(NetData dataToAdd)
        {
            if (numberOfStates < NumberOfRememberedStates)
            {
                SentData[numberOfStates++] = dataToAdd;
            }
            else
            {
                for (int i = 0; i < NumberOfRememberedStates - 1; i++)
                {
                    SentData[i] = SentData[i + 1];
                }
                for (int i = 0; i < Acks.Length - 1; i++)
                {
                    Acks[i] = Acks[i + 1];
                }
                SentData[SentData.Length - 1] = dataToAdd;
            }
        }

        // For acknowledge sent from client
        public int AckData(ushort tickToAck)
        {
            int index = -1;
            for (int i = numberOfStates - 1; i >= 0; i--)
            {
                if (SentData[i].Tick == tickToAck)
                {
                    index = i;
                    AddRtt(i);
                    break;
                }
            }
            return index;
        }

        public void AddRtt(int sendDataIndex)
        {
            if (sendDataIndex < numberOfStates && sendDataIndex >= 0)
            {
                if (rttNumber < Rtts.Length - 1)
                {
                    Rtts[rttNumber++] = (int)(NetData.GetTimeStamp() - SentData[sendDataIndex].TimeStamp);
                }
                else
                {
                    for (int i = 0; i < Rtts.Length - 1; i++)
                    {
                        Rtts[i] = Rtts[i + 1];
                    }
                    Rtts[Rtts.Length - 1] = (int)(NetData.GetTimeStamp() - SentData[sendDataIndex].TimeStamp);
                }
            }
        }

        public double GetAverageRtt()
        {
            int sum = 0;
            for (int i = 0; i < rttNumber; i++)
            {
                sum += Rtts[i];
            }
            return sum / rttNumber;
        }

    }
}
