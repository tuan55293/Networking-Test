using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Assertions.Must;

public class Client : MonoBehaviour
{
    public static Client instance;
    public static int dataBufferSize = 4096;

    public string ip = "127.0.0.1";
    public int port = 26950;
    public int myId = 0;
    public TCP tcp;

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        tcp = new TCP();
        ConnectToServer();
    }

    public void ConnectToServer()
    {
        tcp.Connect();
    }
    private void OnDestroy()
    {
        tcp.socket.Close();
    }

    public class TCP 
    {
        public TcpClient socket;

        private NetworkStream stream;
        private byte[] receiveBuffer;
        public void Connect()
        {
            socket = new TcpClient { ReceiveBufferSize = dataBufferSize, SendBufferSize = dataBufferSize };

            receiveBuffer = new byte[dataBufferSize];
            socket.BeginConnect(instance.ip, instance.port, ConnectCallback, socket);
        }

        private void ConnectCallback(IAsyncResult _result)
        {
            socket.EndConnect(_result);
            if (!socket.Connected)
            {
                Debug.Log("Disconnect");
                return;
            }
            stream = socket.GetStream();
            stream.BeginRead(receiveBuffer, 0, dataBufferSize ,ReceiveCallback, null);
        }

        private void ReceiveCallback(IAsyncResult _result)
        {
            try
            {
                int _byteLength = stream.EndRead(_result);
                if (_byteLength <= 0)
                {
                    // TODO: disconnect
                    Debug.Log("Disconnect");
                    return;
                }
                byte[] _data = new byte[_byteLength];
                Array.Copy(receiveBuffer, _data, _byteLength);

                // TODO: handle data

                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (Exception e) 
            {
                Debug.Log("Disconnect");
                // TODO: disconnect
            }
        }

    }


}

