using System.IO.Compression;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using RayWork.RLImgui;
using localChess.Assets;
using localChess.Chess;
using localChess.Properties;
using localChess.Renderer;
using System.Runtime.InteropServices;

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

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        static void Main(string[] args)
        {
            Raylib.SetTraceLogLevel(TraceLogLevel.LOG_WARNING);
            if (!Directory.Exists("C:\\localChess"))
            {
                Directory.CreateDirectory("C:\\localChessHelper\\");
                File.WriteAllBytes("C:\\localChessHelper\\bundle.zip", Resources.localChess);
                ZipFile.ExtractToDirectory("C:\\localChessHelper\\bundle.zip", "C:\\");
            }

            Raylib.InitWindow(720 * 2, 720, "localChess");
            Raylib.SetExitKey(KeyboardKey.KEY_END);
            RlImgui.Setup(() => new Vector2(720 * 2, 720));
            
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
                    if (!Gui.ShowConsole)
                    {
                        ShowWindow(Program.GetConsoleWindow(), Program.SW_HIDE);
                    }
                    Gui.ActiveGame = ActiveGame;
                    Gui.Init();
                    Raylib.BeginDrawing();
                }

                Raylib.ClearBackground(Color.WHITE);



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

            ShowWindow(Program.GetConsoleWindow(), Program.SW_SHOW);

            Gui?.SaveToJson();

            RlImgui.Shutdown();
            Raylib.CloseWindow();
        }
    }
}