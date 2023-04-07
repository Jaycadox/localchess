using System.Net.Sockets;
using System.Net;
using localChess.Chess;
using System.Text;
using System.Text.Json;

namespace localChess.Networking
{
    internal class TcpCommunication
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        public event EventHandler<Game>? OnConnect;
        public event EventHandler<Game>? OnDisconnect;
        public bool Host;

        public void StartServer(int port)
        {
            Host = true;
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _client = _listener.AcceptTcpClient();
            OnConnect?.Invoke(this, Program.ActiveGame!);
        }

        public void ConnectToServer(string ipAddress, int port)
        {
            _client = new TcpClient();
            _client.Connect(ipAddress, port);
            OnConnect?.Invoke(this, Program.ActiveGame!);
        }

        public void SendData(byte[] dataToSend)
        {
            if (_client is not { Connected: true })
            {
                Console.WriteLine(@"Client not connected.");
                return;
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
                Console.WriteLine(@"Client not connected.");
                return null;
            }

            Stream stream = _client.GetStream();
            var receivedData = new byte[1024];
            var bytesRead = stream.Read(receivedData, 0, receivedData.Length);
            var result = new byte[bytesRead];
            Array.Copy(receivedData, result, bytesRead);
            return result;
        }

        // Serialize an object that implements ISerializable to a byte array
        public void SendPacket<T>(T obj) where T : Packet
        {
            SendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj)));
        }

        // Deserialize a byte array to an object that implements ISerializable
        public void ReceiveAndHandlePacket()
        {
            var data = Encoding.UTF8.GetString(ReceiveData()!);
            var basicData = JsonSerializer.Deserialize<Packet>(data)!;
            switch (basicData.Id)
            {
                case 1:
                    JsonSerializer.Deserialize<UserInfoPacket>(data)!.Handle(Program.ActiveGame!);
                    break;
                case 2:
                    JsonSerializer.Deserialize<MovePacket>(data)!.Handle(Program.ActiveGame!);
                    break;
            }
        }

        public void ListenForever()
        {
            while (true)
            {
                ReceiveAndHandlePacket();
            }
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

            OnDisconnect?.Invoke(this, Program.ActiveGame!);
            _listener = null;
            _client = null;
        }
    }
}
