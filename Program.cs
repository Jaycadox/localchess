﻿using System.Diagnostics;
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
        public static int Size = 720;
        public static Thread? NetworkThread;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            if (IsWindows10OrGreater(17763))
            {
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (IsWindows10OrGreater(18985))
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, (int)attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }

        private static bool IsWindows10OrGreater(int build = -1)
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= build;
        }
        public static void NetworkSetup()
        {
            Network.Communication.OnConnect += (_, _) =>
            {
                Network.Communication.SendPacket(new UserInfoPacket() {Name = Network.Name, PlayingBlack = Network.PrefersBlack, Fen = ActiveGame!.GetFen(), PublicKey = Network.PublicKey });
                Network.SentKeys = true;
            };
        }

        public static void Connect(string ip, int port)
        {
            Gui!.ChatHistory.Clear();
            Gui!.TargetMouseX = -1;
            Gui!.TargetMouseY = -1;
            NetworkThread = new(() =>
            {
                Network.PlayingAgainst = null;
                Network.RegenerateKeys();
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
                }
                Network.Communication.Stop();
                Console.WriteLine(@"[Client] Disconnected from server");
                Raylib.SetWindowTitle("localChess");
                ActiveGame!.LockedColour = null;
                Gui!.FlagHashMismatch = false;
                Gui!.ChatHistory.Clear();
            });
            NetworkThread.Start();
        }

        public static void StartServer(int port)
        {
            Gui!.ChatHistory.Clear();
            Gui!.TargetMouseX = -1;
            Gui!.TargetMouseY = -1;
            NetworkThread = new(() =>
            {
                Network.PlayingAgainst = null;
                Network.RegenerateKeys();
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
                }
                Network.Communication.Stop();
                Console.WriteLine(@"[Server] Disconnected from client");
                Raylib.SetWindowTitle("localChess");
                ActiveGame!.LockedColour = null;
                Gui!.FlagHashMismatch = false;
                Gui!.ChatHistory.Clear();
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

        public static void Main()
        {
            NetworkSetup();
            Raylib.SetTraceLogLevel(TraceLogLevel.LOG_WARNING);
            if (!Directory.Exists("C:\\localChess"))
            {
                Directory.CreateDirectory("C:\\localChessHelper\\");
                File.WriteAllBytes("C:\\localChessHelper\\bundle.zip", Resources.localChess);
                ZipFile.ExtractToDirectory("C:\\localChessHelper\\bundle.zip", "C:\\");
            }

            Raylib.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
            Raylib.SetConfigFlags(ConfigFlags.FLAG_MSAA_4X_HINT);
            Raylib.InitWindow(Size * 2, Size, "localChess");
            Raylib.SetExitKey(KeyboardKey.KEY_END);
            RlImgui.Setup(() => new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
            
            PieceRenderer.Prepare();
            ENet.Library.Initialize();
            unsafe
            {
                var handle = (IntPtr)Raylib.GetWindowHandle();
                UseImmersiveDarkMode(handle, true);
                ShowWindow(handle, SwHide);
                ShowWindow(handle, SwShow);
            }
            

            Raylib.SetTargetFPS(60);
            while (!Raylib.WindowShouldClose())
            {
                Raylib.BeginDrawing();
                Size = Raylib.GetScreenHeight();
                
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
                ActiveGame!.DisplaySize = Size;
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

                

                if (Network.Communication.IsConnected() && new Random().Next(600) == 12 && Network.PlayingAgainst is not null) // Roughly every 10 seconds given 60fps
                {
                    // Application hash check
                    Network.Communication.SendPacket(new HashPacket { Hash = Hash() });
                }
            }

            ShowWindow(Program.GetConsoleWindow(), Program.SwShow);
            ENet.Library.Deinitialize();
            Gui?.SaveToJson();
            Network.Communication.Stop();
            RlImgui.Shutdown();
            Raylib.CloseWindow();
        }
    }
}