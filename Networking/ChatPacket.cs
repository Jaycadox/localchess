using localChess.Chess;

namespace localChess.Networking
{
    internal class ChatPacket : Packet
    {
        public string Content { get; set; } = "";

        public ChatPacket()
        {
            Id = 4;
        }

        public void Handle(Game game)
        {
            var username = Program.Network.PlayingAgainst!;
            if (Content.Trim().Length == 0)
            {
                return;
            }
            Program.Gui!.ChatHistory.Add((username, Content.Trim()));
            Program.Gui!.ScrollChatWindow = true;
        }
    }
}
