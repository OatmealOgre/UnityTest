using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Net = Assets.NetHelper;
using static Assets.NetHelper;
using Assets;

public class NetworkScript : MonoBehaviour
{

    public bool isServer = false;
    public bool autoServer = true;

    public GameObject playerPrefab;

    bool hasStarted = false;

    Server server;
    Client client;

    // Start is called before the first frame update
    void Start()
    {
        //#if UNITY_EDITOR
        //isServer = autoServer || isServer;
        //#endif
        ////Console.WriteLine("Host server? (y/n) ");
        ////isServer = Console.ReadKey(true).Key == ConsoleKey.Y;
        //server = null;
        //client = null;

        //GameState.networkScript = this;

        //if (isServer)
        //{
        //    Destroy(GameObject.Find("ClientMark"));
        //    server = new Server();
        //    server.Start();
        //}
        //else
        //{
        //    client = new Client();
        //    client.Connect("127.0.0.1");
        //}

        //while (true)
        //{
        //    if (!isServer)
        //    {
        //        while (!Console.KeyAvailable)
        //        {
        //            client.Update();
        //            Thread.Sleep(10);
        //        }
        //        ConsoleKey key = Console.ReadKey(true).Key;
        //        client.HandleKeyPress(key);
        //    }
        //    else
        //    {
        //        server.ServerUpdate();
        //    }
        //}
    }

    void StartServerOrClient()
    {
        server = null;
        client = null;

        GameState.networkScript = this;

        if (isServer)
        {
            Destroy(GameObject.Find("ClientMark"));
            server = new Server();
            server.Start();
        }
        else
        {
            client = new Client();
            client.Connect("127.0.0.1");
        }

        hasStarted = true;
    }

    private void OnGUI()
    {
        if (!hasStarted)
        {
            if (GUI.Button(new Rect(100, 150, 100, 40), "Host Server"))
            {
                isServer = true;
                StartServerOrClient();
            }

            if (GUI.Button(new Rect(300, 150, 100, 40), "Run Client"))
            {
                isServer = false;
                StartServerOrClient();
            }
        }
    }

    private void OnApplicationQuit()
    {
        //if (isServer)
        //    server.client.Close();
        //else
        //    client.client.Close();
    }

    // Update is called once per frame
    void Update()
    {
        if (!hasStarted)
            return;

        if (isServer)
            server.ServerUpdate();
        else
        {
            client.HandleKeyPress(ConsoleKey.Enter, Time.deltaTime * 5);
            //if (Input.GetAxis("Vertical") > 0)
            //{
            //    client.HandleKeyPress(ConsoleKey.DownArrow, Time.deltaTime * 5);
            //}
            //if (Input.GetAxis("Vertical") < 0)
            //{
            //    client.HandleKeyPress(ConsoleKey.UpArrow, Time.deltaTime * 5);
            //}
            //if (Input.GetAxis("Horizontal") < 0)
            //{
            //    client.HandleKeyPress(ConsoleKey.LeftArrow, Time.deltaTime * 5);
            //}
            //if (Input.GetAxis("Horizontal") > 0)
            //{
            //    client.HandleKeyPress(ConsoleKey.RightArrow, Time.deltaTime * 5);
            //}
            client.Update();
        }
    }

    internal GameObject CreatePlayerInstance()
    {
        return Instantiate(playerPrefab);
    }
}
