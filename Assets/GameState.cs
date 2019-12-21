using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets
{
    public class GameState 
    {

        static public NetworkScript networkScript;

        BitArray delta;

        public Player[] Players { get; private set; }

        public class Player
        {
            public static readonly int NumberOfFields = typeof(Player).GetFields().Length - 1;

            public ushort id;

            public float x;
            public float y;

            public byte health;

            public Player(ushort id)
            {
                this.id = id;
            }
        }

        public GameState Clone()
        {
            GameState result = new GameState();
            if (Players != null)
            {
                result.Players = new Player[Players.Length];
                for (int i = 0; i < Players.Length; i++)
                {
                    result.Players[i] = new Player(Players[i].id);
                    result.Players[i].health = Players[i].health;
                    result.Players[i].x = Players[i].x;
                    result.Players[i].y = Players[i].y;
                }
            }

            return result;
        }

        public Player GetPlayer(ushort id)
        {
            if (Players == null)
                return null;

            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].id == id)
                    return Players[i];
            }

            return null;
        }

        public Player AddPlayer(ushort id)
        {
            if (Players == null || Players.Length == 0)
            {
                Players = new Player[1];
            }
            else
            {
                Player[] newArray = new Player[Players.Length + 1];
                Players.CopyTo(newArray, 0);
                Players = newArray;
            }

            Players[Players.Length - 1] = new Player(id);
            return Players[Players.Length - 1];
        }

        public void MovePlayer(ushort id, float x, float y)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i] != null && Players[i].id == id)
                {
                    Players[i].x += x;
                    Players[i].y += y;
                    break;
                }

            }
        }

        public void WriteState(BinaryWriter writer)
        {
            int deltaLength = Player.NumberOfFields * Players.Length;
            if (delta == null || delta.Length != deltaLength)
                delta = new BitArray(deltaLength, true);

            writer.Write((byte)Players.Length);

            byte[] deltaBytes = new byte[((delta.Length - 1) / 8) + 1];
            delta.CopyTo(deltaBytes, 0);
            writer.Write(deltaBytes);

            int n = 0;
            for (int i = 0; i < Players.Length; i++)
            {
                try
                {
                    if (delta.Get(n++))
                        writer.Write(Players[i].id);
                    if (delta.Get(n++))
                        writer.Write(Players[i].x);
                    if (delta.Get(n++))
                        writer.Write(Players[i].y);
                    if (delta.Get(n++))
                        writer.Write(Players[i].health);

                }
                catch (Exception)
                {
                    Debug.LogError($"L: {Players.Length} I: {i}");
                    break;

                }
            }
        }
        public static GameState ReadState(BinaryReader reader)
        {
            byte playerCount = reader.ReadByte();
            GameState gameState = new GameState();
            if (playerCount == 0)
                return gameState;

            gameState.Players = new Player[playerCount];
            gameState.delta = new BitArray(reader.ReadBytes((Player.NumberOfFields * playerCount - 1) / 8 + 1));

            int n = 0;
            for (int i = 0; i < playerCount; i++)
            {
                try
                {
                    if (gameState.delta.Get(n++))
                        gameState.Players[i] = new Player(reader.ReadUInt16());
                    if (gameState.delta.Get(n++))
                        gameState.Players[i].x = reader.ReadSingle();
                    if (gameState.delta.Get(n++))
                        gameState.Players[i].y = reader.ReadSingle();
                    if (gameState.delta.Get(n++))
                        gameState.Players[i].health = reader.ReadByte();
                }
                catch (EndOfStreamException)
                {
                    Debug.LogError($"End of stream for {i} - {n}, player count {playerCount}");
                }
            }

            return gameState;
        }

        internal void ToWorld()
        {
            if (Players == null)
                return;
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i] == null)
                    continue;

                GameObject gPlayer = GameObject.Find($"Player{Players[i].id}");
                if (gPlayer == null)
                {
                    gPlayer = networkScript.CreatePlayerInstance();
                    gPlayer.name = $"Player{Players[i].id}";
                }
                gPlayer.transform.position = new Vector3(Players[i].x, Players[i].y);
            }
        }

        internal void RemovePlayer(ushort id)
        {
            Player[] newArray = new Player[Players.Length - 1];
            bool found = false;
            for (int i = 0; i < Players.Length - 1; i++)
            {
                if (Players[i].id == id)
                    found = true;

                if (found)
                    newArray[i] = Players[i + 1];
                else
                    newArray[i] = Players[i];
            }
            Players = newArray;
        }

        public void MakeDeltaFrom(GameState oldState)
        {

        }

    }
}
