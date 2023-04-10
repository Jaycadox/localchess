using System.Numerics;
using System.Security.Cryptography;

namespace localChess.Networking
{
    internal class NetworkConfig
    {
        public string Name { get; set; } = "A localChess user.";
        public bool PrefersBlack { get; set; } = false;
        public TcpCommunication Communication = new();
        public string? PlayingAgainst { get; set; } = null;
        public byte[] PublicKey { get; private set; } = { };
        public byte[]? PeerPublicKey { get; set; }
        public byte[] PrivateKey { get; private set; } = { };
        public bool SentKeys { get; set; } = false;
        public NetworkConfig()
        {
            RegenerateKeys();
        }

        public void RegenerateKeys()
        {
            SentKeys = false;
            PeerPublicKey = null;
            using var rsa = new RSACryptoServiceProvider();
            PublicKey = rsa.ExportRSAPublicKey();
            PrivateKey = rsa.ExportRSAPrivateKey();
        }
    }
}
