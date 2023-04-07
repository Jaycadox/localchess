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
        public Game ActiveGame { get; set; }

        public Piece(PieceType type, bool black, Game game)
        {
            Type = type;
            Black = black;
            ActiveGame = game;
        }

        public Piece Clone()
        {
            return new Piece(Type, Black, ActiveGame)
            {
                MoveCount = MoveCount
            };
        }

        public void RenderAbsolute(int x, int y)
        {
            PieceRenderer.Render(Type, Black, ((float)x / ActiveGame.DisplaySize) * 8.0f, ((float)y / ActiveGame.DisplaySize) * 8.0f, (ActiveGame.DisplaySize / 720.0f) * 90.0f);
        }

        public void Render(float x, float y)
        {
            PieceRenderer.Render(Type, Black, x, y, (ActiveGame.DisplaySize / 720.0f) * 90.0f);
        }
        public void Render(int x, int y)
        {
            Render((float)x, (float)y);
        }
    }
}
