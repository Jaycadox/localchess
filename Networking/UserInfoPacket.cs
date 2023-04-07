using localChess.Chess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace localChess.Networking
{
    internal class UserInfoPacket : Packet
    {
        public string Name { get; set; } = "A localChess user";
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
                game.LockedColour = !PlayingBlack;
            }
            else
            {
                game.LockedColour = Program.Gui?.CfgPrefersBlack;
            }
        }
    }
}
