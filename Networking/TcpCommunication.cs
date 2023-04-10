using System.Net.Sockets;
using System.Net;
using localChess.Chess;
using System.Text;
using System.Text.Json;
using System.IO.Compression;

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
            try
            {
                Host = true;
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _client = _listener.AcceptTcpClient();
                OnConnect?.Invoke(this, Program.ActiveGame!);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Stop();
            }
        }

        public void ConnectToServer(string ipAddress, int port)
        {
            try
            {
                _client = new TcpClient();
                _client.Connect(ipAddress, port);
                OnConnect?.Invoke(this, Program.ActiveGame!);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Stop();
            }
            
        }

        public void SendData(byte[] dataToSend)
        {
            if (_client is not { Connected: true })
            {
                //Console.WriteLine(@"Client not connected.");
                throw new Exception("Client disconnected");
            }

            byte[] compressedBytes;
            using (var ms = new MemoryStream())
            {
                using (var deflateStream = new DeflateStream(ms, CompressionMode.Compress))
                {
                    deflateStream.Write(dataToSend, 0, dataToSend.Length);
                }
                compressedBytes = ms.ToArray();
            }

            Stream stream = _client.GetStream();
            stream.Write(compressedBytes, 0, compressedBytes.Length);
            stream.Flush();
            Console.WriteLine("compression smaller by: " + (dataToSend.Length - compressedBytes.Length));
        }

        public bool IsConnected()
        {
            return _client is { Connected: true } || _listener is not null;
        }

        public byte[]? ReceiveData()
        {
            try
            {
                if (_client is not { Connected: true })
                {
                    //Console.WriteLine(@"Client not connected.");
                    throw new Exception("Client disconnected");
                }

                Stream stream = _client.GetStream();
                var receivedData = new byte[1024];
                var bytesRead = stream.Read(receivedData, 0, receivedData.Length);
                var result = new byte[bytesRead];
                Array.Copy(receivedData, result, bytesRead);
                using var ms = new MemoryStream(result);
                using var deflateStream = new DeflateStream(ms, CompressionMode.Decompress);
                using var decompressedMs = new MemoryStream();
                deflateStream.CopyTo(decompressedMs);
                var decompressedBytes = decompressedMs.ToArray();

                return decompressedBytes;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Stop();
            }
            return null;
        }

        // Serialize an object that implements ISerializable to a byte array
        public void SendPacket<T>(T obj) where T : Packet
        {
            SendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj)));
        }

        // Deserialize a byte array to an object that implements ISerializable
        public void ReceiveAndHandlePacket()
        {
            try
            {
                var byteData = ReceiveData();
                if (byteData is null)
                {
                    throw new Exception("Bad packet");
                }
                var data = Encoding.UTF8.GetString(byteData);
                var basicData = JsonSerializer.Deserialize<Packet>(data)!;
                switch (basicData.Id)
                {
                    case 1:
                        JsonSerializer.Deserialize<UserInfoPacket>(data)!.Handle(Program.ActiveGame!);
                        break;
                    case 2:
                        JsonSerializer.Deserialize<MovePacket>(data)!.Handle(Program.ActiveGame!);
                        break;
                    case 3:
                        JsonSerializer.Deserialize<HashPacket>(data)!.Handle(Program.ActiveGame!);
                        break;
                    case 4:
                        JsonSerializer.Deserialize<ChatPacket>(data)!.Handle(Program.ActiveGame!);
                        break;
                    case 5:
                        JsonSerializer.Deserialize<MousePosPacket>(data)!.Handle(Program.ActiveGame!);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                throw;
            }
        }

        public void ListenForever()
        {
            while (true)
            {
                try
                {
                    ReceiveAndHandlePacket();
                }
                catch (Exception)
                {
                    break;
                }
                
            }
        }

        public void Stop()
        {
            Host = false;
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
