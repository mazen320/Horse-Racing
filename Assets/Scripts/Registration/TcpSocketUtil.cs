using System;
using System.Net.Sockets;

namespace HorseRacing.Registration
{
    static class TcpSocketUtil
    {
        /// <summary>
        /// Keep NAT mappings alive during long idle periods (kiosk/tablet setups).
        /// </summary>
        public static void ConfigureAcceptedClient(TcpClient tcp, int sendTimeoutMs, int receiveTimeoutMs)
        {
            if (tcp?.Client == null)
                return;

            tcp.NoDelay = true;
            tcp.SendTimeout = sendTimeoutMs;
            tcp.ReceiveTimeout = receiveTimeoutMs;
            EnableKeepAlive(tcp.Client, keepAliveSeconds: 60, keepAliveIntervalSeconds: 10);
        }

        public static void EnableKeepAlive(Socket socket, int keepAliveSeconds = 60, int keepAliveIntervalSeconds = 10)
        {
            if (socket == null)
                return;

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var timeMs = (uint)Math.Max(1, keepAliveSeconds) * 1000u;
                var intervalMs = (uint)Math.Max(1, keepAliveIntervalSeconds) * 1000u;
                var bytes = new byte[12];
                BitConverter.GetBytes(1u).CopyTo(bytes, 0);
                BitConverter.GetBytes(timeMs).CopyTo(bytes, 4);
                BitConverter.GetBytes(intervalMs).CopyTo(bytes, 8);
                socket.IOControl(IOControlCode.KeepAliveValues, bytes, null);
            }
            catch
            {
                // Keep basic KeepAlive=true if platform tuning fails.
            }
#endif
        }
    }
}
