using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace HorseRacing.Registration
{
    /// <summary>
    /// TCP server compatible with the Registration tablet app (UNetComm / EasyTcp4 length-prefixed JSON).
    /// </summary>
    public sealed class RegistrationTcpServer : MonoBehaviour
    {
        const string PingValidCheck = "ValidPing";
        const string ClientPingMsg = "ClientToServerPing";
        const string ServerPingMsg = "ServerToClientPing";

        [SerializeField] bool autoStart = true;
        [SerializeField] string listenAddress = "0.0.0.0";
        [SerializeField] int port = 1234;
        [SerializeField] int discoveryPort = 3738;
        [SerializeField] float broadcastIntervalMinutes = 5f;
        [SerializeField] bool logTraffic;
        [SerializeField] int clientPingTimeoutSeconds = 15;
        [SerializeField] int socketSendTimeoutMs = 3000;
        [SerializeField] int socketReceiveTimeoutMs = 65000;

        readonly ConcurrentQueue<Action> _mainThread = new();
        readonly ConcurrentQueue<ClientState> _pendingRemovals = new();
        readonly Dictionary<string, ClientState> _clients = new();
        readonly object _clientLock = new();

        TcpListener _listener;
        Thread _acceptThread;
        UdpClient _discovery;
        Timer _broadcastTimer;
        Timer _maintenanceTimer;
        volatile bool _running;
        bool _registered;
        RegisterEntryData _lastRegistration = new();

        public event Action<RegisterEntryData> RegistrationReceived;
        public event Action StartCommandReceived;
        public event Action RestartCommandReceived;
        public event Action EndGameCommandReceived;
        public event Action NewRaceCommandReceived;
        public event Action<bool> ClientConnectionChanged;

        public bool HasRegistration => _registered;
        public RegisterEntryData LastRegistration => _lastRegistration;

        public bool HasConnectedClient
        {
            get
            {
                lock (_clientLock)
                    return _clients.Count > 0;
            }
        }

        void Start()
        {
            if (autoStart)
                StartServer();

            _maintenanceTimer = new Timer(_ => MaintenanceTick(), null, 1000, 1000);
        }

        void SendKeepAlive() => SendKeepAliveInternal();

        void OnDestroy() => StopServer();

        void Update()
        {
            while (_pendingRemovals.TryDequeue(out var staleClient))
                RemoveClient(staleClient);

            while (_mainThread.TryDequeue(out var action))
                action?.Invoke();
        }

        public void StartServer()
        {
            if (_running)
                return;

            _running = true;
            _registered = false;
            _lastRegistration = new RegisterEntryData();

            _listener = new TcpListener(ParseAddress(listenAddress), port);
            _listener.Start();
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            _acceptThread.Start();

            StartDiscoveryBroadcast();
            SendRestart();
            // SendRestart goes to tablets only; keep the PC UI in sync when the listener boots.
            EnqueueMain(() => RestartCommandReceived?.Invoke());

            if (logTraffic)
                Debug.Log($"[RegistrationTcpServer] Listening on {listenAddress}:{port}, discovery UDP {discoveryPort}");
        }

        public void StopServer()
        {
            _running = false;

            _maintenanceTimer?.Dispose();
            _maintenanceTimer = null;

            _broadcastTimer?.Dispose();
            _broadcastTimer = null;

            try { _discovery?.Close(); } catch { /* ignored */ }
            _discovery = null;

            try { _listener?.Stop(); } catch { /* ignored */ }

            lock (_clientLock)
            {
                foreach (var client in _clients.Values)
                    client.Dispose();
                _clients.Clear();
            }
        }

        public void SendRestart()
        {
            var payload = new RegisterEntryData { restart = true };
            Broadcast(payload);
        }

        public void SendRegistrationAck(RegisterEntryData data)
        {
            Broadcast(data);
        }

        void SyncRegistrationToClient(ClientState client)
        {
            if (!_registered || _lastRegistration.entries == null || _lastRegistration.entries.Count == 0)
                return;

            var ack = _lastRegistration;
            ack.registered = true;
            ack.SetTime();
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ack));

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (SendTo(client, bytes) && logTraffic)
                    Debug.Log($"[RegistrationTcpServer] Re-synced registration to {client.Key}");
            });
        }

        public void SendPinging()
        {
            SendKeepAliveInternal();
        }

        /// <summary>
        /// Echo the start back the moment it is accepted. The raceStarted broadcast only lands after
        /// the countdown, which is too late for the tablet's resend watchdog.
        /// </summary>
        void BroadcastStartAck()
        {
            Broadcast(new RegisterEntryData { start = true });
        }

        void MaintenanceTick()
        {
            if (!_running)
                return;

            var timedOut = new List<ClientState>();
            lock (_clientLock)
            {
                foreach (var client in _clients.Values)
                {
                    client.PingTimeout--;
                    if (client.PingTimeout <= 0)
                        timedOut.Add(client);
                }
            }

            foreach (var client in timedOut)
                QueueRemoveClient(client);

            SendKeepAliveInternal();
        }

        void SendKeepAliveInternal()
        {
            if (!_running)
                return;

            var pingBytes = Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(new PingData
                {
                    validCheck = PingValidCheck,
                    msg = ServerPingMsg
                }));
            var appBytes = Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(new RegisterEntryData { pinging = true }));

            foreach (var client in SnapshotClients())
            {
                if (SendTo(client, pingBytes))
                    SendTo(client, appBytes);
            }
        }

        void QueueRemoveClient(ClientState client)
        {
            if (client == null)
                return;

            _pendingRemovals.Enqueue(client);
        }

        /// <summary>
        /// Single write path for every sender. Serializes frames per client and evicts a client
        /// whose socket blocks or errors, so a dead tablet can never stall the caller twice.
        /// </summary>
        bool SendTo(ClientState client, byte[] payload)
        {
            if (client == null)
                return false;

            try
            {
                if (!client.Tcp.Connected)
                {
                    QueueRemoveClient(client);
                    return false;
                }

                lock (client.WriteLock)
                    WriteMessage(client.Stream, payload);

                return true;
            }
            catch (Exception ex)
            {
                if (logTraffic)
                    Debug.LogWarning($"[RegistrationTcpServer] Send failed ({client.Key}): {ex.Message}");
                QueueRemoveClient(client);
                return false;
            }
        }

        List<ClientState> SnapshotClients()
        {
            lock (_clientLock)
                return new List<ClientState>(_clients.Values);
        }

        public void BroadcastRaceStarted(long raceStartUtcTicks)
        {
            Broadcast(new RegisterEntryData
            {
                raceStarted = true,
                raceStartUtcTicks = raceStartUtcTicks
            });

            if (logTraffic)
                Debug.Log($"[RegistrationTcpServer] Race started broadcast (utc ticks {raceStartUtcTicks})");
        }

        public void BroadcastRaceEnded(long raceEndUtcTicks)
        {
            Broadcast(new RegisterEntryData
            {
                raceEnded = true,
                raceEndUtcTicks = raceEndUtcTicks
            });

            if (logTraffic)
                Debug.Log($"[RegistrationTcpServer] Race ended broadcast (utc ticks {raceEndUtcTicks})");
        }

        void StartDiscoveryBroadcast()
        {
            try
            {
                _discovery = new UdpClient { EnableBroadcast = true };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RegistrationTcpServer] UDP discovery disabled: {ex.Message}");
                return;
            }

            var intervalMs = Mathf.Max(1f, broadcastIntervalMinutes) * 60_000f;
            _broadcastTimer = new Timer(_ => BroadcastServerInfo(), null, 0, (int)intervalMs);
        }

        void BroadcastServerInfo()
        {
            if (!_running || _discovery == null)
                return;

            try
            {
                var info = new ServerConnectionInfo
                {
                    valid = true,
                    ip = GetLocalIpAddress(),
                    port = port
                };
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(info));
                _discovery.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, discoveryPort));
            }
            catch (Exception ex)
            {
                if (logTraffic)
                    Debug.LogWarning($"[RegistrationTcpServer] Discovery broadcast failed: {ex.Message}");
            }
        }

        void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var tcpClient = _listener.AcceptTcpClient();
                    TcpSocketUtil.ConfigureAcceptedClient(tcpClient, socketSendTimeoutMs, socketReceiveTimeoutMs);
                    var stream = tcpClient.GetStream();
                    stream.ReadTimeout = socketReceiveTimeoutMs;
                    stream.WriteTimeout = socketSendTimeoutMs;
                    var endpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                    var key = endpoint != null ? $"{endpoint.Address}:{endpoint.Port}" : Guid.NewGuid().ToString();

                    var state = new ClientState(tcpClient, stream, key)
                    {
                        PingTimeout = clientPingTimeoutSeconds
                    };
                    lock (_clientLock)
                        _clients[key] = state;

                    EnqueueMain(() =>
                    {
                        ClientConnectionChanged?.Invoke(true);
                        if (logTraffic)
                            Debug.Log($"[RegistrationTcpServer] Client connected: {key}");
                        SyncRegistrationToClient(state);
                    });

                    var readThread = new Thread(() => ReadLoop(state)) { IsBackground = true };
                    readThread.Start();
                }
                catch (SocketException)
                {
                    if (!_running)
                        break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.LogWarning($"[RegistrationTcpServer] Accept error: {ex.Message}");
                }
            }
        }

        void ReadLoop(ClientState client)
        {
            try
            {
                while (_running && client.Tcp.Connected)
                {
                    byte[] payload;
                    try
                    {
                        payload = ReadMessage(client.Stream);
                    }
                    catch (IOException)
                    {
                        // Receive timeout while idle — keep listening until ping timeout evicts the client.
                        continue;
                    }

                    if (payload == null)
                    {
                        if (logTraffic)
                            Debug.LogWarning($"[RegistrationTcpServer] Dropping {client.Key}: stream ended or frame length invalid");
                        break;
                    }

                    lock (_clientLock)
                    {
                        if (_clients.ContainsKey(client.Key))
                            _clients[client.Key].PingTimeout = clientPingTimeoutSeconds;
                    }

                    HandlePayload(client, payload);
                }
            }
            catch (Exception ex)
            {
                if (_running && logTraffic)
                    Debug.LogWarning($"[RegistrationTcpServer] Read error ({client.Key}): {ex.Message}");
            }
            finally
            {
                RemoveClient(client);
            }
        }

        void HandlePayload(ClientState client, byte[] payload)
        {
            var json = Encoding.UTF8.GetString(payload);

            if (TryParsePing(json))
            {
                lock (_clientLock)
                {
                    if (_clients.ContainsKey(client.Key))
                        _clients[client.Key].PingTimeout = clientPingTimeoutSeconds;
                }

                // Registration tablet (EasyTcp) requires ServerToClientPing or it drops after ~5s.
                var reply = Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new PingData
                    {
                        validCheck = PingValidCheck,
                        msg = ServerPingMsg
                    }));
                SendTo(client, reply);
                return;
            }

            if (!TryDeserialize(json, out var data) || data == null)
                return;

            EnqueueMain(() => ProcessMessage(data));
        }

        static bool TryDeserialize(string json, out RegisterEntryData data)
        {
            try
            {
                data = JsonConvert.DeserializeObject<RegisterEntryData>(json);
                return data != null;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        static bool TryParsePing(string json)
        {
            try
            {
                var ping = JsonConvert.DeserializeObject<PingData>(json);
                return ping != null && ping.validCheck == PingValidCheck && ping.msg == ClientPingMsg;
            }
            catch
            {
                return false;
            }
        }

        void ProcessMessage(RegisterEntryData data)
        {
            if (data.pinging || data.raceStarted || data.raceEnded)
                return;

            if (data.restart)
            {
                _registered = false;
                _lastRegistration = new RegisterEntryData();
                RestartCommandReceived?.Invoke();
                return;
            }

            if (data.newRace)
            {
                if (!_registered || _lastRegistration.entries == null || _lastRegistration.entries.Count == 0)
                {
                    Debug.LogWarning("[RegistrationTcpServer] New race ignored — no active registration on server");
                    return;
                }

                NewRaceCommandReceived?.Invoke();
                if (logTraffic)
                    Debug.Log("[RegistrationTcpServer] New race (same players) received");
                return;
            }

            if (data.endGame)
            {
                _registered = false;
                _lastRegistration = new RegisterEntryData();
                EndGameCommandReceived?.Invoke();
                if (logTraffic)
                    Debug.Log("[RegistrationTcpServer] End game command received");
                return;
            }

            if (data.entries != null && data.entries.Count > 0)
            {
                _registered = true;
                _lastRegistration = data;
                _lastRegistration.SetTime();
                _lastRegistration.registered = true;
                SendRegistrationAck(_lastRegistration);
                RegistrationReceived?.Invoke(_lastRegistration);

                if (logTraffic)
                    Debug.Log($"[RegistrationTcpServer] Registered {data.entries.Count} player(s)");

                if (data.start)
                {
                    BroadcastStartAck();
                    StartCommandReceived?.Invoke();
                }

                return;
            }

            if (data.start)
            {
                if (!_registered || _lastRegistration.entries == null || _lastRegistration.entries.Count == 0)
                {
                    Debug.LogWarning("[RegistrationTcpServer] Start ignored — no active registration on server");
                    return;
                }

                BroadcastStartAck();
                StartCommandReceived?.Invoke();
                if (logTraffic)
                    Debug.Log("[RegistrationTcpServer] Start command received");
            }
        }

        void RemoveClient(ClientState client)
        {
            if (client == null)
                return;

            var removed = false;
            lock (_clientLock)
                removed = _clients.Remove(client.Key);

            if (!removed)
                return;

            client.Dispose();

            EnqueueMain(() =>
            {
                ClientConnectionChanged?.Invoke(_clients.Count > 0);
                if (logTraffic)
                    Debug.Log($"[RegistrationTcpServer] Client disconnected: {client.Key}");
            });
        }

        void Broadcast(RegisterEntryData data)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
            var snapshot = SnapshotClients();
            if (snapshot.Count == 0)
                return;

            // Writes block until the socket send timeout, so never do them on the render thread:
            // a tablet that died during idle would otherwise stall the game for seconds.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (var client in snapshot)
                    SendTo(client, bytes);
            });
        }

        void EnqueueMain(Action action) => _mainThread.Enqueue(action);

        static byte[] ReadMessage(NetworkStream stream)
        {
            var lengthBuffer = new byte[4];
            if (!ReadExact(stream, lengthBuffer, 4))
                return null;

            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > 1_000_000)
                return null;

            var payload = new byte[length];
            return ReadExact(stream, payload, length) ? payload : null;
        }

        static void WriteMessage(NetworkStream stream, byte[] payload)
        {
            var length = BitConverter.GetBytes(payload.Length);
            stream.Write(length, 0, length.Length);
            stream.Write(payload, 0, payload.Length);
        }

        static bool ReadExact(NetworkStream stream, byte[] buffer, int count)
        {
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    return false;
                offset += read;
            }

            return true;
        }

        static IPAddress ParseAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || address == "0.0.0.0")
                return IPAddress.Any;
            return IPAddress.Parse(address);
        }

        static string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch
            {
                // ignored
            }

            return "127.0.0.1";
        }

        sealed class ClientState : IDisposable
        {
            public ClientState(TcpClient tcp, NetworkStream stream, string key)
            {
                Tcp = tcp;
                Stream = stream;
                Key = key;
            }

            public TcpClient Tcp { get; }
            public NetworkStream Stream { get; }
            public string Key { get; }
            public int PingTimeout = 5;

            /// <summary>Pings, acks and broadcasts come from different threads; frames must not interleave.</summary>
            public readonly object WriteLock = new();

            public void Dispose()
            {
                try { Stream?.Close(); } catch { /* ignored */ }
                try { Tcp?.Close(); } catch { /* ignored */ }
            }
        }
    }
}
