using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Shadowsocks.Controller
{
    class Dns2Socks5 : ProxySocketTun
    {
        public Dns2Socks5(IPEndPoint proxy, string user, string password, int timeout = 5000)
            : base(proxy.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            GetSocket().SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, timeout);
            GetSocket().SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, timeout);

            var ar = BeginConnect(proxy, null, null);
            ar.AsyncWaitHandle.WaitOne(timeout);
            if (!ar.IsCompleted)
            {
                throw new SocketException((int) SocketError.TimedOut);
            }
            EndConnect(ar);

            if (!ConnectSocks5ProxyServer(proxy.Address.ToString(), proxy.Port, true, user, password))
            {
                throw new SocketException((int) SocketError.ConnectionReset);
            }

            UdpSocket = new Socket(proxy.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            UdpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, timeout);
            UdpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, timeout);
        }

        public Socket UdpSocket { get; }

        public int SendTo(byte[] buffer, EndPoint target)
        {

            List<byte> dataSock5Send = new List<byte>();
            dataSock5Send.Add(0);
            dataSock5Send.Add(0);
            dataSock5Send.Add(0);


            IPEndPoint ipEndPoint = target as IPEndPoint;
            if (ipEndPoint == null)
            {
                throw new ArgumentException("Must be IPEndPoint!", nameof(target));
            }

            byte[] addBytes = ipEndPoint.Address.GetAddressBytes();
            if (addBytes.GetLength(0) > 4)
            {
                dataSock5Send.Add(4); // IPv6
                for (int i = 0; i < 16; ++i)
                {
                    dataSock5Send.Add(addBytes[i]);
                }
            }
            else
            {
                dataSock5Send.Add(1); // IPv4
                for (int i = 0; i < 4; ++i)
                {
                    dataSock5Send.Add(addBytes[i]);
                }
            }

            var port = ipEndPoint.Port;
            dataSock5Send.Add((byte) (port / 256));
            dataSock5Send.Add((byte) (port % 256));

            dataSock5Send.AddRange(buffer);

            return UdpSocket.SendTo(dataSock5Send.ToArray(), _remoteUDPEndPoint);
        }

        public int Receive(byte[] buffer)
        {
            EndPoint endPoint = new IPEndPoint(_remoteUDPEndPoint.Address, _remoteUDPEndPoint.Port);

            int bytesRead = UdpSocket.ReceiveFrom(buffer, ref endPoint);

            if (bytesRead < 7)
            {
                throw new SocketException((int) SocketError.ConnectionReset);
            }

            if (buffer[3] == 1)
            {
                int head = 3 + 1 + 4 + 2;
                bytesRead = bytesRead - head;
                Array.Copy(buffer, head, buffer, 0, bytesRead);
            }
            else if (buffer[3] == 4)
            {
                int head = 3 + 1 + 16 + 2;
                bytesRead = bytesRead - head;
                Array.Copy(buffer, head, buffer, 0, bytesRead);
            }
            else if (buffer[3] == 3)
            {
                int head = 3 + 1 + 1 + buffer[4] + 2;
                bytesRead = bytesRead - head;
                Array.Copy(buffer, head, buffer, 0, bytesRead);
            }
            else
            {
                throw new SocketException((int) SocketError.ConnectionReset);
            }

            return bytesRead;
        }

        public override void Shutdown(SocketShutdown how)
        {
            UdpSocket.Shutdown(how);
            base.Shutdown(how);
        }

        public override void Close()
        {
            UdpSocket.Close();
            base.Close();
        }
    }
}
