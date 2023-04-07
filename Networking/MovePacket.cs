using localChess.Chess;

namespace localChess.Networking
{
    internal class MovePacket : Packet
    {
        public int FromIndex { get; set; }
        public int ToIndex { get; set; }

        public MovePacket()
        {
            Id = 2;
        }

        public void Handle(Game game)
        {
            var p = game.Board[FromIndex];
            if (p is not null && (game.LockedColour == null || p.Black == !game.LockedColour))
            {
                game.PerformMove(new Move(FromIndex, ToIndex));
            }
        }
    }
}
