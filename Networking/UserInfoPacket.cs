using localChess.Chess;

namespace localChess.Networking
{
    internal class UserInfoPacket : Packet
    {
        public string Name { get; set; } = "A localChess user";
        public string Fen { get; set; } = "";

        public bool? PlayingBlack { get; set; } = null;

        public UserInfoPacket()
        {
            Id = 1;
        }

        public void Handle(Game game)
        {
            Program.Network.PlayingAgainst = Name;

            if (!Program.Network.Communication.Host)
            {
                Program.ActiveGame = Game.FromFen(Fen);
                Program.Gui!.ActiveGame = Program.ActiveGame;
                Program.ActiveGame.LockedColour = !PlayingBlack;
            }
            else
            {
                Program.ActiveGame!.LockedColour = Program.Gui?.CfgPrefersBlack;
            }
        }
    }
}
