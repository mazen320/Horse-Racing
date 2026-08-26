using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using Newtonsoft.Json;

using UTool.TabSystem;
using UTool.Utility;
using System.Runtime.CompilerServices;

namespace UTool.Networking
{
    public class UNetComm : MonoBehaviour
    {
        public static Dictionary<TabName, UNetComm> _instance = new Dictionary<TabName, UNetComm>();

        public static UNetComm GetInstance(TabName tabName)
        {
            if (_instance.ContainsKey(tabName))
                return _instance[tabName];
            else
                return null;
        }

        [SerializeField] private Tab tab;

        [SpaceArea]

        [SerializeField] public bool connectedToServer = false;

        [SpaceArea]

        [SerializeField][BeginGroup][Disable] private bool runService;
        [SerializeField][Disable] private bool serverService;
        [SerializeField][Disable] private bool getServerIP;
        [SerializeField][Disable] private string ip;
        [SerializeField][Disable] private int port;
        [SerializeField][Disable] private ServerConnectionInfo serverInfo = new ServerConnectionInfo();
        [SerializeField][Disable] private bool tcpProtocol;
        [SerializeField][EndGroup][Disable] private bool debugLog;

        [SpaceArea]

        [SerializeField] private float tickRatePerSec = 60;
        [SerializeField] private float broadcastServerInfoPerMin = 5;
        //[SerializeField] private float requestEverySec = 2;

        [SpaceArea]

        [SerializeField][ReorderableList][Disable] private List<UNetCommLink> uNetCommLinks = new List<UNetCommLink>();

        [Line(5), SpaceArea]

        [SerializeField] public UnityEvent<string, bool> OnConnectionToServer = new UnityEvent<string, bool>();
        [SerializeField] public UnityEvent<string, bool> OnClientConnection = new UnityEvent<string, bool>();
        [SerializeField] public UnityEvent<string, byte[]> OnDataReceived = new UnityEvent<string, byte[]>();

        private UDPHandler broadcastUDP = new UDPHandler();

        private UDPHandler udp = new UDPHandler();
        private TCPHandler tcp = new TCPHandler();

        //private UDPHandler backendUDP = new UDPHandler();

        private Queue<(string, bool)> connectionToServerEventQueue = new Queue<(string, bool)>();
        private Queue<(string, bool)> clientConnectionEventQueue = new Queue<(string, bool)>();
        private Queue<(string, byte[])> receivedDataEventQueue = new Queue<(string, byte[])>();
        private Queue<(string, byte[])> receivedServerInfoEventQueue = new Queue<(string, byte[])>();

        public bool isServer => serverService;

        private UTimer tickTimer = new UTimer();
        private UTimer broadcastTimer = new UTimer();
        //private UTimer subTickTimer = new UTimer();

        public void MidAwake()
        {
            if (_instance.ContainsKey(tab.tTabName))
            {
                Destroy(_instance[tab.tTabName]);
                _instance[tab.tTabName] = this;
            }
            else
                _instance.Add(tab.tTabName, this);
        }

        private void Start()
        {
            tickTimer.Start();
        }

        private void Update()
        {
            tickTimer.TickPerSec(tickRatePerSec, onTick: Tick);
            //subTickTimer.TickPerMin(requestEverySec, onTick: RequestServerIP);

            if (runService && serverService && getServerIP)
                broadcastTimer.TickPerMin(broadcastServerInfoPerMin, onTick: BroadcastServerInfo);
        }

        private void SetupBroadcast()
        {
            if (!getServerIP)
                return;

            broadcastUDP.OnDataReceived.AddListener((ipPort, data) =>
            {
                receivedServerInfoEventQueue.Enqueue((ipPort, data));
            });

            broadcastUDP.SetIpPort(3738);
            broadcastUDP.StartListenServer();

            broadcastTimer.Start();
        }

        private void BroadcastServerInfo()
        {
            serverInfo = new ServerConnectionInfo();

            serverInfo.ip = UUtility.GetLocalIPAddress();
            serverInfo.port = port;
            serverInfo.valid = true;

            string jsonData = JsonConvert.SerializeObject(serverInfo);
            broadcastUDP.Broadcast(jsonData.ToUTF8());
        }

        private void Tick()
        {
            if (connectionToServerEventQueue.TryDequeue(out (string ipPort, bool connectionType) serverConnectionState))
                try
                {
                    OnConnectionToServerDispatched(serverConnectionState.ipPort, serverConnectionState.connectionType);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

            if (clientConnectionEventQueue.TryDequeue(out (string ipPort, bool connectionType) clientConnectionState))
                try
                {
                    OnClientConnectionDispatched(clientConnectionState.ipPort, clientConnectionState.connectionType);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

            if (receivedDataEventQueue.TryDequeue(out (string ipPort, byte[] data) receivedDataState))
                try
                {
                    UUtility.NetLog($"Data Received{Environment.NewLine}{receivedDataState.data.ToUTF8String()}", debugLog);
                    OnDataReceivedDispatched(receivedDataState.ipPort, receivedDataState.data);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

            if (receivedServerInfoEventQueue.TryDequeue(out (string ipPort, byte[] data) receivedInfoState))
                try
                {
                    OnServerInfoReceivedDispatched(receivedInfoState.ipPort, receivedInfoState.data);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

            //if (getServerIP && !serverService)
            //{
            //    BackendUDPData bdUDP = new BackendUDPData();
            //    bdUDP.dataType = BackendUDPData.DataType.ServerIPRequest;
            //    bdUDP.serverIPInfo = new IPInfo(UUtility.GetLocalIPAddress(), port);

            //    string jsonData = JsonConvert.SerializeObject(bdUDP);
            //    backendUDP.Broadcast(jsonData.ToUTF8());
            //}
            //backendUDP.Broadcast();

            if (tcpProtocol)
                tcp.Tick();
        }

        private void OnServerInfoReceivedDispatched(string ipPort, byte[] data)
        {
            if (!runService || serverService || !getServerIP)
                return;

            ServerConnectionInfo receivedConnectionInfo;

            try
            {
                receivedConnectionInfo = JsonConvert.DeserializeObject<ServerConnectionInfo>(data.ToUTF8String());
            }
            catch
            {
                return;
            }

            if (receivedConnectionInfo == null)
                return;

            if (receivedConnectionInfo.Same(serverInfo))
                return;

            serverInfo = receivedConnectionInfo;

            RestartServer();
        }

        private void OnConnectionToServerDispatched(string ipPort, bool connectionType)
        {
            foreach(UNetCommLink link in uNetCommLinks)
                link.OnConnectionToServer?.Invoke(ipPort, connectionType);

            OnConnectionToServer?.Invoke(ipPort, connectionType);
        }

        private void OnClientConnectionDispatched(string ipPort, bool connectionType)
        {
            foreach (UNetCommLink link in uNetCommLinks)
                link.OnClientConnection?.Invoke(ipPort, connectionType);

            OnClientConnection?.Invoke(ipPort, connectionType);
        }

        private void OnDataReceivedDispatched(string ipPort, byte[] data)
        {
            foreach (UNetCommLink link in uNetCommLinks)
                link.OnDataReceived?.Invoke(ipPort, data);

            OnDataReceived?.Invoke(ipPort, data);
        }

        private void OnApplicationQuit()
        {
            //backendUDP.StopListenServer();
            StopService();
        }

        public void OnValueChange(VariableUpdateType updateType, TVariable variable)
        {
            if (updateType == VariableUpdateType.Changed)
                return;

            if (variable.tVariableName == TabVariableName.ServiceMode)
                runService = variable.boolValue;

            if (variable.tVariableName == TabVariableName.ServiceType)
                serverService = variable.boolValue;

            if (variable.tVariableName == TabVariableName.GetServerIP)
                getServerIP = variable.boolValue;

            if (variable.tVariableName == TabVariableName.IP)
                ip = variable.stringValue.Trim();

            if (variable.tVariableName == TabVariableName.Port)
                port = variable.intValue;

            if (variable.tVariableName == TabVariableName.Protocol)
                tcpProtocol = variable.boolValue;

            if (variable.tVariableName == TabVariableName.DebugLog)
                debugLog = variable.boolValue;
        }

        public void OnTabLoaded()
        {
            udp.log = debugLog;
            tcp.log = debugLog;

            udp.OnDataReceived.AddListener(DataReceived);
            tcp.OnDataReceived.AddListener(DataReceived);

            tcp.OnClientConnection.AddListener(ClientConnection);
            tcp.OnConnectionToServer.AddListener(ServerConnection);

            SetupBroadcast();
            StartService();
        }

        public void OnTabSaved()
        {
            udp.log = debugLog;
            tcp.log = debugLog;

            if(!getServerIP && broadcastUDP != null)
            {
                broadcastUDP.StopListenServer();
                broadcastUDP = null;
            }

            RestartServer();
        }

        public void StartService()
        {
            if (!runService)
                return;

            if (tcpProtocol)
                StartTCPService();
            else
                StartUDPService();

            //if (getServerIP)
            //{
            //    backendUDP.StartListenServer();
            //}
            //else
            //{
            //    if (tcpProtocol)
            //        StartTCPService();
            //    else
            //        StartUDPService();
            //}
        }

        public void StopService()
        {
            udp.StopListenServer();
            tcp.StopService();
        }

        public void RestartServer()
        {
            StopService();
            StartService();
        }

        private void StartTCPService()
        {
            if (getServerIP)
                tcp.SetEndPoint(serverInfo.ip, serverInfo.port);
            else
                tcp.SetEndPoint(ip, port);

            if (serverService)
            {
                if (getServerIP)
                    tcp.SetEndPoint(UUtility.GetLocalIPAddress(), port);

                tcp.StartServer();
            }
            else
            {
                if (getServerIP && !serverInfo.valid)
                    return;

                tcp.ConnectToServer();
            }
        }

        private void StartUDPService()
        {
            if (getServerIP)
                udp.SetIpPort(serverInfo.ip, serverInfo.port);
            else
                udp.SetIpPort(ip, port);

            if (serverService)
                udp.StartListenServer();
        }

        public void Send(byte[] data)
        {
            if (tcpProtocol)
            {
                if (serverService)
                    tcp.SendToAllClient(data);
                else
                    tcp.Send(data);
            }
            else
                udp.Send(data);
        }

        public void Broadcast(byte[] data)
        {
            if (!tcpProtocol)
                udp.Broadcast(data);
        }

        public void SendToTCPClient(string ipPort, byte[] data)
        {
            tcp.SendClient(ipPort, data);
        }

        public void AddLink(UNetCommLink uNetCommLink)
        {
            uNetCommLinks.Add(uNetCommLink);
        }

        public void RemoveLink(UNetCommLink uNetCommLink)
        {
            uNetCommLinks.Remove(uNetCommLink);
        }

        private void ServerConnection(string ipPort, bool connectionType)
        {
            connectedToServer = connectionType;
            connectionToServerEventQueue.Enqueue((ipPort, connectionType));
        }

        private void ClientConnection(string ipPort, bool connectionType)
        {
            clientConnectionEventQueue.Enqueue((ipPort, connectionType));
        }

        private void DataReceived(string ipPort, byte[] data)
        {
            receivedDataEventQueue.Enqueue((ipPort, data));
        }

        private class BackendUDPData
        {
            public DataType dataType;

            public IPInfo serverIPInfo;
            public IPInfo senderIPInfo;

            public enum DataType
            {
                ServerIPRequest
            }
        }
    }  

    public class IPInfo
    {
        public string ip;
        public int port;

        public IPInfo(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }
    }

    [System.Serializable]
    public class ServerConnectionInfo
    {
        public bool valid;
        public string ip;
        public int port;

        public bool Same(ServerConnectionInfo b)
        {
            if(b == null)
                return false;

            return (ip == b.ip) && (port == b.port);
        }
    }
}