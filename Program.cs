using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using Raylib_cs;
using RayWork.RLImgui;
using localChess.Chess;
using localChess.Properties;
using localChess.Renderer;
using System.Runtime.InteropServices;
using localChess.Networking;
using System.Security.Cryptography;

namespace localChess
{
    internal class Program
    {
        public static Game? ActiveGame;
        public static NetworkConfig Network = new();
        public static Gui? Gui;

        public static void Reset()
        {
            try
            {
                UciEngine.StockfishProcess?.Kill();
            }
            catch (Exception)
            {
                // ignored
            }

            UciEngine.StockfishProcess = null;
            ActiveGame = new();
            Gui = Gui.LoadFromJson();
            Gui.ActiveGame = ActiveGame;

            Gui.Init();
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public const int SwHide = 0;
        public const int SwShow = 5;
        public static bool ShowGui = true;
        public const int Size = 720;
        public static Thread? NetworkThread;

        public static void NetworkSetup()
        {
            Network.Communication.OnConnect += (_, _) =>
            {
                Network.Communication.SendPacket(new UserInfoPacket() {Name = Network.Name, PlayingBlack = Network.PrefersBlack, Fen = ActiveGame!.GetFen()});
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
                    Raylib.SetWindowTitle("(not so) localChess");
                    Network.Communication.ListenForever();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    Network.Communication.Stop();
                }
                Console.WriteLine(@"[Client] Disconnected from server");
                Raylib.SetWindowTitle("localChess");
                ActiveGame!.LockedColour = null;
                Gui!.FlagHashMismatch = false;
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
                    Raylib.SetWindowTitle("(not so) localChess");
                    Network.Communication.ListenForever();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    Network.Communication.Stop();
                }
                Console.WriteLine(@"[Server] Disconnected from client");
                Raylib.SetWindowTitle("localChess");
                ActiveGame!.LockedColour = null;
                Gui!.FlagHashMismatch = false;
            });
            NetworkThread.Start();
        }

        public static string Hash()
        {
            var path = Process.GetCurrentProcess().MainModule?.FileName!;
            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(path);
            return Convert.ToBase64String(sha256.ComputeHash(fileStream));
        }

        static void Main()
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
                if (Raylib.IsKeyPressed(KeyboardKey.KEY_F1))
                {
                    ShowGui = !ShowGui;
                    Raylib.SetWindowSize(ShowGui ? Size * 2 : Size, Size);
                }
                Raylib.DrawText("Loading...", 0, 0, 64, Color.WHITE);
                if (ActiveGame is null)
                {
                    Raylib.EndDrawing();
                    ActiveGame = new Game();
                    Gui = Gui.LoadFromJson();
                    if (!Gui.ShowConsole)
                    {
                        ShowWindow(Program.GetConsoleWindow(), Program.SwHide);
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

                

                if (Network.Communication.IsConnected() && new Random().Next(600) == 12) // Roughly every 10 seconds given 60fps
                {
                    // Application hash check
                    Network.Communication.SendPacket(new HashPacket { Hash = Hash() });
                }
            }

            ShowWindow(Program.GetConsoleWindow(), Program.SwShow);

            Gui?.SaveToJson();
            Network.Communication.Stop();
            RlImgui.Shutdown();
            Raylib.CloseWindow();
        }
    }
}