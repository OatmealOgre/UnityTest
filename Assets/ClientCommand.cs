using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets
{
    public class ClientCommand
    {
        public ClientCommand()
        {
            Id = idCounter++;
            CreationTimeStamp = NetData.GetTimeStamp();
        }
        private static ushort idCounter = 0;

        public ushort Id { get; set; }

        public float Duration { get; set; }

        public bool HasBeenSimulated { get; set; } // local - do not send or handle on server

        public long CreationTimeStamp { get; set; } // local - do not send or handle on server

        public bool Up { get; set; }
        public bool Down { get; set; }
        public bool Left { get; set; }
        public bool Right { get; set; }


        private static readonly int NumberOfBoolData = typeof(ClientCommand).GetProperties().Length - 4;


        public void Perform(GameState inState, ushort playerId)
        {
            if (Duration > 1)
            {
                throw new Exception($"Duration too great! {DateTime.Now.ToString()}");
            }

            if (Right && !Left)
            {
                inState.MovePlayer(playerId, 1 * Duration, 0);
            }
            else if (!Right && Left)
            {
                inState.MovePlayer(playerId, -1 * Duration, 0);
            }

            if (Up && !Down)
            {
                inState.MovePlayer(playerId, 0, -1 * Duration);
            }
            else if (!Up && Down)
            {
                inState.MovePlayer(playerId, 0, 1 * Duration);
            }
        }

        public static void Write(ClientCommand[] commands, BinaryWriter writer)
        {
            if (commands == null || commands.Length == 0)
            {
                writer.Write((byte)0);
                return;
            }

            writer.Write((byte)commands.Length);

            for (int i = 0; i < commands.Length; i++)
            {
                writer.Write(commands[i].Id);
                writer.Write(commands[i].Duration);

                BitArray bits = new BitArray(new bool[]
                {
                    commands[i].Up,
                    commands[i].Down,
                    commands[i].Left,
                    commands[i].Right
                });
                int test = (NumberOfBoolData - 1) / 8 + 1;
                byte[] bytes = new byte[(NumberOfBoolData - 1) / 8 + 1];
                bits.CopyTo(bytes, 0);

                writer.Write(bytes);
            }
        }

        public static ClientCommand[] ReadCommands(BinaryReader reader)
        {
            byte count = reader.ReadByte();
            if (count == 0)
                return null;

            ClientCommand[] result = new ClientCommand[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = new ClientCommand();

                result[i].Id = reader.ReadUInt16();
                result[i].Duration = reader.ReadSingle();

                // Get each bit for the buttons
                byte[] bytes = reader.ReadBytes((NumberOfBoolData - 1) / 8 + 1);
                BitArray bits = new BitArray(bytes);
                result[i].Up = bits[0];
                result[i].Down = bits[1];
                result[i].Left = bits[2];
                result[i].Right = bits[3];

            }

            return result;
        }

        public override bool Equals(object obj)
        {
            ClientCommand c = obj as ClientCommand;
            if (c == null)
                return false;

            return Id == c.Id;
        }
    }


}
