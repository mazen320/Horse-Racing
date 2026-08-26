using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

using UTool.Utility;

using EasyTcp4;
using EasyTcp4.ServerUtils;
using EasyTcp4.ClientUtils;
using EasyTcp4.ClientUtils.Async;

using DG.Tweening;

namespace UTool.Networking
{
    public class TCPHandler
    {
        private string serverPingMsg = "ServerToClientPing";
        private string clientPingMsg = "ClientToServerPing";
        private string pingValidCheck = "ValidPing";

        private string ip;
        private int port;

        private EasyTcpServer tcpServer;
        private EasyTcpClient tcpClient;

        private bool connecting = false;
        private Tween reConnectionTween;

        public bool serverRunning = false;
        public bool log = false;

        public UnityEvent<string, bool> OnConnectionToServer = new UnityEvent<string, bool>();
        public UnityEvent<string, bool> OnClientConnection = new UnityEvent<string, bool>();
        public UnityEvent<string, byte[]> OnDataReceived = new UnityEvent<string, byte[]>();

        public Dictionary<string, List<byte>> accumulatingData = new Dictionary<string, List<byte>>();

        private bool reConnectToServer = false;

        UTimer pingTimer = new UTimer();

        public Dictionary<string, NetEntityData> connectedNetEntity = new Dictionary<string, NetEntityData>();

        private bool connectedToServer;
        //private bool connectedToServer => tcpClient == null ? false : tcpClient.IsConnected();

        public bool isServer => tcpServer != null;
        public bool isClient => tcpClient != null;

        public void SetEndPoint(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public void Tick()
        {
            if (reConnectToServer)
            {
                reConnectToServer = false;

                StopService();
                ConnectToServer();
            }

            //if (!connecting)
            //    if (tcpClient != null)
            //        Debug.Log(connectedToServer);

            //if (tcpServer != null)
            //    Debug.Log(tcpServer.ConnectedClientsCount);

            pingTimer.TickPerSec(1, null, PingUpdate);
        }

        private void PingUpdate()
        {
            List<NetEntityData> netEntityDatas = new List<NetEntityData>(connectedNetEntity.Values);
            foreach (NetEntityData netEntityData in netEntityDatas)
            {
                netEntityData.timeout--;

                if (netEntityData.timeout <= 0)
                {
                    if (netEntityData.client != null)
                    {
                        ClientConnection(false, netEntityData.client);
                        netEntityData.client.Dispose();
                    }
                    else
                        connectedNetEntity.Remove(netEntityData.ipPort);
                }
            }

            PingData pingData = new PingData();
            pingData.validCheck = pingValidCheck;

            if (isServer && serverRunning)
            {
                pingData.msg = serverPingMsg;
                SendToAllClient(pingData.ToJsonString().ToUTF8());
            }

            if (isClient && connectedToServer)
            {
                pingData.msg = clientPingMsg;
                Send(pingData.ToJsonString().ToUTF8());
            }
        }

        #region TCP Server

        public void StartServer()
        {
            if (serverRunning)
            {
                ServerLog("Server Already Running");
                return;
            }

            tcpServer = new EasyTcpServer();

            tcpServer.OnConnect += (o, client) => ClientConnection(connected: true, client);
            tcpServer.OnDisconnect += (o, client) => ClientConnection(connected: false, client);

            tcpServer.OnDataReceive += OnDataReceive;

            tcpServer.OnError += (o, e) => ServerLog(e.ToString(), 2);

            Task.Run(() =>
            {
                tcpServer.Start(ip, (ushort)port);
            });

            serverRunning = true;

            ServerLog("Server Started");
        }

        private void ClientConnection(bool connected, EasyTcpClient client)
        {
            IPEndPoint clientAddress = client.GetEndPoint();
            string ipPort = connected ? $"{clientAddress.Address}:{clientAddress.Port}" : connectedNetEntity.Where(x => x.Value.client == client).First().Key;

            ServerLog($"Client '{ipPort}' {(connected ? "Connected" : "Disconnected")}");

            if (connected)
            {
                NetEntityData netEntityData = new NetEntityData();
                netEntityData.ipPort = ipPort;
                netEntityData.client = client;
                netEntityData.timeout = 5;

                connectedNetEntity.Add(ipPort, netEntityData);
            }
            else
            {
                connectedNetEntity.Remove(ipPort);
            }

            OnClientConnection?.Invoke(ipPort, connected);
        }

        public void SendToAllClient(byte[] data)
        {
            if (tcpServer.GetConnectedClients().Count == 0)
                return;

            try
            {
                tcpServer.SendAll(data);
            }
            catch (Exception ex)
            {
                ServerLog($"Failed to Send Data To All Clients : {ex}", 1);
            }
        }

        public void SendClient(string ipPort, byte[] data)
        {
            EasyTcpClient clientToSend = tcpServer.GetConnectedClients().Find(x => SameIPAddress(x.GetEndPoint(), ipPort));

            if (clientToSend == null)
            {
                ServerLog($"Unable to Find Client With ID '{ipPort}'", 1);
                return;
            }

            Task.Run(() =>
            {
                clientToSend.SendArray(data);
            });

            ServerLog($"Sending Data To Client : {ipPort}");
        }

        #endregion

        #region TCP Client

        public void ConnectToServer()
        {
            if (connectedToServer)
            {
                ClientLog("Already Connected to Server");
                return;
            }

            if (connecting)
                return;

            tcpClient = new EasyTcpClient();

            tcpClient.OnConnect += (o, client) => ServerConnection(connected: true, client);
            tcpClient.OnDisconnect += (o, client) => ServerConnection(connected: false, client);

            tcpClient.OnDataReceive += OnDataReceive;

            tcpClient.OnError += (o, e) => ClientLog(e.ToString(), 2); ;

            TryConnectingToServer();
        }

        private async void TryConnectingToServer()
        {
            //if (connecting)
            //    return;

            ClientLog($"Connecting To Server '{ip}:{port}'");

            connecting = true;
            bool connected = await tcpClient.ConnectAsync(ip, (ushort)port);
            connecting = false;

            if (!connected)
            {
                ClientLog("Failed To Connect to Server");

                RetryServerConnection();
            }
        }

        private void RetryServerConnection()
        {
            ClientLog("Retrying Connection in 1 Seconds");

            reConnectionTween.KillTween();
            reConnectionTween = DOVirtual.DelayedCall(1, () =>
            {
                ClientLog("Re-Connecting To Server");
                reConnectToServer = true;
            });
        }

        private IPEndPoint GetIPEndPoint()
        {
            IPAddress.TryParse(ip, out IPAddress address);
            return new IPEndPoint(address, port);
        }

        private void ServerConnection(bool connected, EasyTcpClient server)
        {
            connectedToServer = connected;

            IPEndPoint serverAddress = server != null ? server.GetEndPoint() : GetIPEndPoint();
            string ipPort = connected ? $"{serverAddress.Address}:{serverAddress.Port}" : connectedNetEntity.Where(x => x.Value.client == server).First().Key;

            ClientLog($"{(connected ? "Connected To" : "Disconnected From")} Server '{ipPort}'");

            if (connected)
            {
                NetEntityData netEntityData = new NetEntityData();
                netEntityData.ipPort = ipPort;
                netEntityData.client = server;
                netEntityData.timeout = 5;

                connectedNetEntity.Add(ipPort, netEntityData);
            }
            else
            {
                connectedNetEntity.Remove(ipPort);

                //if (log)
                //    Debug.Log($"[TCP Client] - Retrying Connection To Server '{ipPort}'");

                RetryServerConnection();
            }

            OnConnectionToServer?.Invoke(ipPort, connected);
        }

        public void Send(byte[] data)
        {
            Task.Run(() =>
            {
                tcpClient.SendArray(data);
            });

            ClientLog($"Sending Data");
        }

        #endregion

        public void StopService()
        {
            if (tcpServer != null)
            {
                tcpServer.Dispose();
                tcpServer = null;
            }

            if (tcpClient != null)
            {
                tcpClient.Dispose();
                tcpClient = null;
            }

            serverRunning = false;
            connecting = false;

            reConnectionTween.KillTween();
            //if (reConnectionTween.KillTween())
            //    ClientLog("Re-Connecting To Server Stopped");

            UUtility.NetLog("TCP Service Stop", log);
        }

        public bool SameIPAddress(IPEndPoint endPoint, string ipPort)
        {
            string ipPort0 = $"{endPoint.Address}:{endPoint.Port}";
            return ipPort0 == ipPort;
        }

        private void OnDataReceive(object sender, Message message)
        {
            IPEndPoint messageAddress = message.Client.GetEndPoint();
            string ipPort = $"{messageAddress.Address}:{messageAddress.Port}";

            if (message.Data.ToUTF8String().FromJsonString(out PingData pingData))
                if (pingData.validCheck == pingValidCheck)
                {
                    if ((isServer && (pingData.msg == clientPingMsg)) ||
                        (isClient && (pingData.msg == serverPingMsg)))
                        connectedNetEntity[ipPort].timeout = 5;

                    return;
                }

            OnDataReceived?.Invoke(ipPort, message.Data);
        }

        private void ServerLog(string message, int logType = 0)
        {
            UUtility.NetLog(logType, $"[TCP Server] {message}", log);
        }

        private void ClientLog(string message, int logType = 0)
        {
            UUtility.NetLog(logType, $"[TCP Client] {message}", log);
        }

        public class NetEntityData
        {
            public EasyTcpClient client;

            public string ipPort;
            public int timeout = 5;
        }

        private class PingData
        {
            public string validCheck = "";
            public string msg;
        }
    }
}