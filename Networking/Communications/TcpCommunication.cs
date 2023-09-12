using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using localChess.Chess;

namespace localChess.Networking.Communications
{
    internal class TcpCommunication : ICommunication
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        public event EventHandler<Game>? OnConnect;
        public event EventHandler<Game>? OnDisconnect;
        public bool Host;
        public void StartServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Console.WriteLine("Waiting for client...");
            _client = _listener.AcceptTcpClient();
            Console.WriteLine("Got client");

        }

        public void ConnectToServer(string ipAddress, int port)
        {
            _client = new TcpClient();
            Console.WriteLine($"Attempting to connect to {ipAddress}:{port}");
            _client.Connect(ipAddress, port);
            Console.WriteLine("Connected");
        }

        public void SendData(byte[] dataToSend)
        {
            if (_client is not { Connected: true })
            {
                throw new Exception("Client disconnected");
            }

            Stream stream = _client.GetStream();
            stream.Write(dataToSend, 0, dataToSend.Length);
            stream.Flush();
        }

        public bool IsConnected()
        {
            return _client is { Connected: true } || _listener is not null;
        }

        public byte[]? ReceiveData()
        {
            if (_client is not { Connected: true })
            {
                throw new Exception("Client disconnected");
            }

            Stream stream = _client.GetStream();
            var receivedData = new byte[2048];
            var bytesRead = stream.Read(receivedData, 0, receivedData.Length);
            var result = new byte[bytesRead];
            Array.Copy(receivedData, result, bytesRead);

            return result;
        }

        public void Stop()
        {
            try
            {
                _listener?.Stop();
                _client?.Close();
            }
            catch (Exception)
            {
                Console.WriteLine(
                    @"An error occurred while forcibly terminating the connection. We're doing it the hard way.");
            }
            _listener = null;
            _client = null;
        }
    }
}
