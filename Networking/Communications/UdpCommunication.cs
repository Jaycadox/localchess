using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ENet;
using localChess.Chess;
using System.Threading.Channels;

namespace localChess.Networking.Communications
{
    internal class UdpCommunication : ICommunication
    {
        private Host? server;
        private Host? client;
        private Peer? peer;

        public event EventHandler<Game>? OnConnect;
        public event EventHandler<Game>? OnDisconnect;
        public bool Host;
        public void StartServer(int port)
        {
            Host = true;
            server = new Host();
            Address address = new Address
            {
                Port = (ushort)port,
            };
            address.SetHost("0.0.0.0");
            server.Create(address, 2);
            while (true)
            {
                ENet.Event netEvent = default(ENet.Event);
                server.Service(0, out netEvent);

                if (netEvent.Type == EventType.Connect)
                {
                    Console.WriteLine("Client connected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                    peer = netEvent.Peer;
                    return;
                }
            }
        }

        public void ConnectToServer(string ipAddress, int port)
        {
            client = new Host();
            Address address = new Address();
            address.SetHost(ipAddress);
            address.Port = (ushort)port;
            client.Create();
            peer = client.Connect(address);
        }

        public void SendData(byte[] dataToSend)
        {
            ENet.Packet packet = default(ENet.Packet);
            byte[] data = new byte[1024];
            packet.Create(data);
            peer?.Send(0, ref packet);
        }

        public bool IsConnected()
        {
            return (server is not null) || (peer is not null);
        }

        public byte[]? ReceiveData()
        {
            if (server is not null)
            {
                while (true)
                {
                    if (server.CheckEvents(out var netEvent) <= 0)
                    {
                        server.Service(15, out netEvent);
                    }

                    if (netEvent.Type == EventType.Receive)
                    {
                        Console.WriteLine("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP + ", Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);
                        var buffer = new byte[1024];
                        netEvent.Packet.CopyTo(buffer);
                        return buffer;
                    }

                }

            } else
            {
                while (true)
                {
                    if (client.CheckEvents(out var netEvent) <= 0)
                    {
                        client.Service(15, out netEvent);
                    }

                    if (netEvent.Type == EventType.Receive)
                    {
                        Console.WriteLine("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP + ", Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);
                        var buffer = new byte[1024];
                        netEvent.Packet.CopyTo(buffer);
                        return buffer;
                    }
                }
                
            }
        }

        public void Stop()
        {
            try
            {
                peer?.DisconnectNow(0);
                client?.Dispose();
                server?.Dispose();
            }
            catch (Exception)
            {
                Console.WriteLine(
                    @"An error occurred while forcibly terminating the connection. We're doing it the hard way.");
            }
            client = null;
            peer = null;
            server = null;
        }
    }
}
