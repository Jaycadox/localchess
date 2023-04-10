using Raylib_cs;
using System.Numerics;
using localChess.Assets;
using localChess.Chess;

namespace localChess.Renderer
{
    internal class PieceRenderer : IRenderable
    {
        public PieceType Type { get; }
        public bool Black { get; }
        private static Dictionary<PieceType, PieceRenderer> WhitePieceRenderers = new();
        private static Dictionary<PieceType, PieceRenderer> BlackPieceRenderers = new();
        private static Texture2D PieceSpritesheet;

        private PieceRenderer(PieceType type, bool black)
        {
            Type = type;
            Black = black;
        }

        public static void Prepare()
        {
            foreach (PieceType type in Enum.GetValues(typeof(PieceType)))
            {
                WhitePieceRenderers.Add(type, new PieceRenderer(type, false));
                BlackPieceRenderers.Add(type, new PieceRenderer(type, true));
            }

            PieceSpritesheet = Raylib.LoadTexture(AssetLoader.GetPath("Pieces.png"));
        }

        public static void Render(PieceType type, bool black, float x, float y, float pieceScale, float alpha = 1.0f)
        {
            var (finalX, finalY) = (x * pieceScale, y * pieceScale);

            var dict = black ? BlackPieceRenderers : WhitePieceRenderers;
            var renderer = dict[type];
            renderer.RenderScaled((int) finalX, (int) finalY, pieceScale, alpha);

        }

        public void RenderScaled(int x, int y, float pieceScale, float alpha = 1.0f)
        {
            var sheetX = (int)Type * 90;
            var sheetY = Black ? 90 : 0;

            Raylib.DrawTextureTiled(PieceSpritesheet,
                new Rectangle(sheetX, sheetY, 90, 90),
                new Rectangle(x, y, pieceScale, pieceScale),
                Vector2.Zero, 0, pieceScale / 90.0f, new Color(255, 255, 255, (int)(255.0f * alpha)));
        }

        public void Render(int x, int y)
        {
            RenderScaled(x, y, 90);
        }
    }
}
