using System.Net.Sockets;
using System.Net;
using localChess.Chess;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using localChess.Networking.Communications;

namespace localChess.Networking
{
    internal class Communication
    {
        public event EventHandler<Game>? OnConnect;
        public event EventHandler<Game>? OnDisconnect;
        public bool Host;
        public TcpCommunication Protocol = new();

        public void StartServer(int port)
        {
            try
            {
                Host = true;
                Protocol.StartServer(port);
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
                Protocol.ConnectToServer(ipAddress, port);
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
            byte[] compressedBytes;
            using (var ms = new MemoryStream())
            {
                using (var deflateStream = new DeflateStream(ms, CompressionMode.Compress))
                {
                    deflateStream.Write(dataToSend, 0, dataToSend.Length);
                }
                compressedBytes = ms.ToArray();
            }

            if (Program.Network.PeerPublicKey is not null && Program.Network.SentKeys)
            {
                using var rsaEncrypt = new RSACryptoServiceProvider();
                rsaEncrypt.ImportRSAPublicKey(Program.Network.PeerPublicKey!, out var bytesRead);
                compressedBytes = rsaEncrypt.Encrypt(compressedBytes, false);
            }
            Protocol.SendData(compressedBytes);
        }

        public byte[]? ReceiveData()
        {
            try
            {
                var result = Protocol.ReceiveData()!;
                if (Program.Network.PeerPublicKey is not null && Program.Network.SentKeys)
                {
                    using var rsaEncrypt = new RSACryptoServiceProvider();
                    rsaEncrypt.ImportRSAPrivateKey(Program.Network.PrivateKey, out var bytesRead_);
                    result = rsaEncrypt.Decrypt(result, false);
                }

                using var ms = new MemoryStream(result);
                using var deflateStream = new DeflateStream(ms, CompressionMode.Decompress);
                using var decompressedMs = new MemoryStream();
                deflateStream.CopyTo(decompressedMs);
                var decompressedBytes = decompressedMs.ToArray();
                Console.WriteLine("got: " + Encoding.Unicode.GetString(decompressedBytes));
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
            Console.WriteLine("sent: " + JsonSerializer.Serialize(obj));
            SendData(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(obj)));
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
                var data = Encoding.Unicode.GetString(byteData);
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

        public bool IsConnected()
        {
            return Protocol.IsConnected();
        }

        public void Stop()
        {
            Host = false;
            Protocol.Stop();
            OnDisconnect?.Invoke(this, Program.ActiveGame!);
        }
    }
}
