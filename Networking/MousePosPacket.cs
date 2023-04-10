using localChess.Chess;

namespace localChess.Networking
{
    internal class MousePosPacket : Packet
    {
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;

        public MousePosPacket()
        {
            Id = 5;
        }

        public void Handle(Game game)
        {
            if (Program.Gui!.TargetMouseX == X && Program.Gui!.TargetMouseY == Y)
            {
                return;
            }
            Program.Gui!.TargetMouseX = X;
            Program.Gui!.TargetMouseY = Y;
            if (Program.Gui!.MouseX == -1)
            {
                Program.Gui!.MouseX = X;
            }
            if (Program.Gui!.MouseY == -1)
            {
                Program.Gui!.MouseY = Y;
            }
            Program.Gui!.MouseOpacity = 255;
        }
    }
}
