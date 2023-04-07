using localChess.Chess;

namespace localChess.Networking
{
    internal class HashPacket : Packet
    {
        public string Hash { get; set; }

        public HashPacket()
        {
            Id = 3;
        }

        public void Handle(Game game)
        {
            if (Hash != Program.Hash())
            {
                Program.Gui!.FlagHashMismatch = true;
            }
        }
    }
}
