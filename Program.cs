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
using localChess.Networking;

namespace localChess
{
    internal class Program
    {
        public static Game? ActiveGame = null;
        public static NetworkConfig Network = new();
        public static Gui? Gui = null;

        public static void Reset()
        {
            try
            {
                UCIEngine.StockfishProcess?.Kill();
            } catch(Exception) {}
            
            UCIEngine.StockfishProcess = null;
            ActiveGame = new();
            Gui = Gui.LoadFromJson();
            Gui.ActiveGame = ActiveGame;

            Gui.Init();
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;
        public static bool ShowGui = true;
        public const int Size = 720;
        public static Thread? NetworkThread = null;

        public static void NetworkSetup()
        {
            Network.Communication.OnConnect += (_, game) =>
            {
                Network.Communication.SendPacket(new UserInfoPacket() {Name = Network.Name, PlayingBlack = Network.PrefersBlack});
            };
        }

        public static void Connect(string ip, int port)
        {
            NetworkThread = new(() =>
            {
                Network.PlayingAgainst = null;
                try
                {
                    Network.Communication.ConnectToServer(ip, port);
                    Console.WriteLine(@"[Client] Connected to server");
                    Network.Communication.ListenForever();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    Network.Communication.Stop();
                }
                Console.WriteLine(@"[Client] Disconnected from server");
                ActiveGame!.LockedColour = null;
            });
            NetworkThread.Start();
        }

        public static void StartServer(int port)
        {
            NetworkThread = new(() =>
            {
                Network.PlayingAgainst = null;
                try
                {
                    Network.Communication.StartServer(port);
                    Console.WriteLine(@"[Server] Connected to client");
                    Network.Communication.ListenForever();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    Network.Communication.Stop();
                }
                Console.WriteLine(@"[Server] Disconnected from client");
                ActiveGame!.LockedColour = null;
            });
            NetworkThread.Start();
        }

        static void Main(string[] args)
        {
            NetworkSetup();
            Raylib.SetTraceLogLevel(TraceLogLevel.LOG_WARNING);
            if (!Directory.Exists("C:\\localChess"))
            {
                Directory.CreateDirectory("C:\\localChessHelper\\");
                File.WriteAllBytes("C:\\localChessHelper\\bundle.zip", Resources.localChess);
                ZipFile.ExtractToDirectory("C:\\localChessHelper\\bundle.zip", "C:\\");
            }

            Raylib.InitWindow(Size * 2, Size, "localChess");
            Raylib.SetExitKey(KeyboardKey.KEY_END);
            RlImgui.Setup(() => new Vector2(Raylib.GetScreenWidth(), Size));
            
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
                    Gui = Gui.LoadFromJson();
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
                    Gui.PostRender();
                }

                Raylib.EndDrawing();

                if (Raylib.IsKeyPressed(KeyboardKey.KEY_F1))
                {
                    ShowGui = !ShowGui;
                    Raylib.SetWindowSize(ShowGui ? Size * 2 : Size, Size);
                }
            }

            ShowWindow(Program.GetConsoleWindow(), Program.SW_SHOW);

            Gui?.SaveToJson();
            Network.Communication.Stop();
            RlImgui.Shutdown();
            Raylib.CloseWindow();
        }
    }
}