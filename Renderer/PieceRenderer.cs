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

        public static void Render(PieceType type, bool black, float x, float y)
        {
            var (finalX, finalY) = (x * 90.0f, y * 90.0f);

            var dict = black ? BlackPieceRenderers : WhitePieceRenderers;
            var renderer = dict[type];
            renderer.Render((int) finalX, (int) finalY);

        }
        public void Render(int x, int y)
        {
            var sheetX = (int)Type * 90;
            var sheetY = Black ? 90 : 0;

            Raylib.DrawTexturePro(PieceSpritesheet,
                new Rectangle(sheetX, sheetY, 90, 90),
                new Rectangle(x, y, 90, 90),
                Vector2.Zero, 0, Color.WHITE);
        }
    }
}
