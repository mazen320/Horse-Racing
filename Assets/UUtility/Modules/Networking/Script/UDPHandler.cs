using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System;
using UnityEngine;
using UnityEngine.Events;
using System.Threading;
using System.Threading.Tasks;

using UTool.Utility;

using SimpleUdp;

namespace UTool.Networking
{
    public class UDPHandler
    {
        UdpEndpoint udp;

        public bool log;

        public string ip;
        public int port;

        public UnityEvent<string, byte[]> OnDataReceived = new UnityEvent<string, byte[]>();

        public void SetIpPort(int port) => SetIpPort(IPAddress.Any.ToString(), port);
        public void SetIpPort(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public void StartListenServer()
        {
            udp = new UdpEndpoint(ip, port);

            udp.DatagramReceived += DatagramReceived;

            //udp.EndpointDetected += EndpointDetected;
        }

        public void StopListenServer()
        {
            if (udp != null)
                udp.Dispose();
        }

        public void Send(byte[] data)
        {
            Send(ip, port, data);
        }

        public void Broadcast(byte[] data)
        {
            Send(IPAddress.Broadcast.ToString(), port, data);
        }

        private void Send(string ip, int port, byte[] data)
        {
            if (udp == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    udp.Send(ip, port, data);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }

        private void EndpointDetected(object sender, EndpointMetadata md)
        {
            ServerLog("Endpoint detected: " + md.Ip + ":" + md.Port);
        }

        private void DatagramReceived(object sender, Datagram dg)
        {
            string ipPort = $"{dg.Ip}:{dg.Port}";
            OnDataReceived?.Invoke(ipPort, dg.Data);

            ServerLog("[" + dg.Ip + ":" + dg.Port + "]: " + dg.Data.ToUTF8String());
        }

        private void ServerLog(string message, int logType = 0)
        {
            UUtility.NetLog(logType, $"[UDP Server] {message}", log);
        }

        private void ClientLog(string message, int logType = 0)
        {
            UUtility.NetLog(logType, $"[UDP Client] {message}", log);
        }
    }
}