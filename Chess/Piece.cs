using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using localChess.Renderer;

namespace localChess.Chess
{
    internal class Piece : IRenderable
    {
        public PieceType Type { get; set; }
        public int MoveCount { get; set; }
        public bool Black { get; }

        public Piece(PieceType type, bool black)
        {
            Type = type;
            Black = black;
        }

        public Piece Clone()
        {
            return new Piece(Type, Black)
            {
                MoveCount = MoveCount
            };
        }

        public void RenderAbsolute(int x, int y)
        {
            PieceRenderer.Render(Type, Black, ((float)x / 720.0f) * 8.0f, ((float)y / 720.0f) * 8.0f);
        }

        public void Render(int x, int y)
        {
            PieceRenderer.Render(Type, Black, x, y);
        }
    }
}
