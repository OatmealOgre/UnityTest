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

    public List<string> errorMessages;

    public bool isServer = false;
    public bool autoServer = true;

    public GameObject playerPrefab;

    bool hasStarted = false;

    Server server;
    Client client;

    private void OnEnable()
    {
        //errorMessages = new List<string>();
        //Application.logMessageReceived += OnLogException;
        //errorMessages.Add("Activated!");
    }

    private void OnDisable()
    {
        //Application.logMessageReceived -= OnLogException;
    }

    private void OnLogException(string _message, string _stackTrace, LogType _logType)
    {
        if (_logType == LogType.Exception)
        {
            errorMessages.Add(_message);
            if (Application.isEditor)
            {
                // Only break in editor to allow examination of the current scene state.
                //Debug.Break();
            }
            else
            {
                // There's no standard way to return an error code to the OS,
                // so just quit regularly.
                //Application.Quit();
            }
        }
    }

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
            Debug.Log($"Client begin connect. {DateTime.Now.ToString()}");
            client.Connect("127.0.0.1");
        }

        hasStarted = true;
    }

    private void WindowFunc(int id)
    {
        GUI.Label(new Rect(65, 45, 200, 30), errorMessages[id]);

        if (GUI.Button(new Rect(60, 150, 75, 30), "OK"))
        {
            errorMessages.RemoveAt(id);
        }
        if (GUI.Button(new Rect(165, 150, 75, 30), "Close All"))
        {
            errorMessages = new List<string>();
        }
    }

    private void OnGUI()
    {
        //var oldcol = GUI.color;
        //var oldbg = GUI.backgroundColor;
        //var oldcont = GUI.contentColor;
        
        //GUI.color = Color.yellow;
        //GUI.backgroundColor = new Color(255,255,255,1);
        //GUI.contentColor = Color.red;
        
        //GUI.skin.window.normal.background = Texture2D.whiteTexture;
        //GUI.skin.window.onNormal.background = Texture2D.whiteTexture;
        //if (errorMessages.Count > 0)
        //{
        //    for (int i = 0; i < errorMessages.Count; i++)
        //    {
        //        GUI.Window(i, new Rect((Screen.width / 2) - 150, (Screen.height / 2) - 175, 300, 250), WindowFunc, "Exception occurred!");
        //    }
        //}
        //GUI.color = oldcol;
        //GUI.backgroundColor = oldbg;
        //GUI.contentColor = oldcont;
        
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
        GameObject p1 = GameObject.Find($"Player0");
        GUI.Label(new Rect(20, 20, 100, 40), $"Pos x: {p1?.transform.position.x}");
        GUI.Label(new Rect(20, 35, 100, 40), $"Pos y: {p1?.transform.position.y}");
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
