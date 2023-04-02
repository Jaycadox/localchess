using System.IO.Compression;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using RayWork.RLImgui;
using localChess.Assets;
using localChess.Chess;
using localChess.Properties;
using localChess.Renderer;

namespace localChess
{
    internal class Program
    {
        public static Game? ActiveGame = null;
        public static GUI? Gui = null;

        public static void Reset()
        {
            try
            {
                UCIEngine.StockfishProcess?.Kill();
            } catch(Exception) {}
            
            UCIEngine.StockfishProcess = null;
            ActiveGame = new();
            Gui = GUI.LoadFromJson();
            Gui.ActiveGame = ActiveGame;

            Gui.Init();
        }

        static void Main(string[] args)
        {
            if (!Directory.Exists("C:\\localChess"))
            {
                Directory.CreateDirectory("C:\\localChessHelper\\");
                File.WriteAllBytes("C:\\localChessHelper\\bundle.zip", Resources.localChess);
                ZipFile.ExtractToDirectory("C:\\localChessHelper\\bundle.zip", "C:\\");
            }

            Raylib.InitWindow(720 * 2, 720, "localChess");
            Raylib.SetExitKey(KeyboardKey.KEY_END);
            RlImgui.Setup(() => new Vector2(720 * 2, 720));
            Texture2D board = Raylib.LoadTexture(AssetLoader.GetPath("Board.png"));
            PieceRenderer.Prepare();

            
            
            Raylib.SetTargetFPS(60);
            while (!Raylib.WindowShouldClose())
            {
                Raylib.BeginDrawing();
                Raylib.DrawText("Loading...", 0, 0, 64, Color.WHITE);
                if (ActiveGame is null)
                {
                    Raylib.EndDrawing();
                    ActiveGame = new Game();
                    Gui = GUI.LoadFromJson();
                    Gui.ActiveGame = ActiveGame;
                    Gui.Init();
                    Raylib.BeginDrawing();
                }

                Raylib.ClearBackground(Color.WHITE);
                Raylib.DrawTexture(board, 0, 0, Color.WHITE);
                


                ActiveGame.OnTick();
                ActiveGame.Render(0, 0);
                if (Gui is not null)
                {
                    RlImgui.Begin();
                    Gui.OnFrame();
                    RlImgui.End();
                }
                
                Raylib.EndDrawing();
            }

            Gui?.SaveToJson();

            RlImgui.Shutdown();
            Raylib.CloseWindow();
        }
    }
}