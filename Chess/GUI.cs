﻿using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ImGuiNET;
using localChess.Networking;
using localChess.Networking.Communications;
using Mono.Nat;
using Raylib_CsLo;

namespace localChess.Chess
{
    internal class Gui
    {
        [JsonIgnore]
        public Game? ActiveGame { get; set; }

        [JsonInclude]
        public bool Opened = true;
        [JsonInclude]
        public bool GameInfoOpened = true;

        [JsonIgnore]
        private float _currentEval;

        [JsonIgnore]
        public bool FlagHashMismatch;

        private string? _opening = "...";
        private HttpClient? _client;
        private readonly List<string> _moveList = new();
        private List<List<string>> _bestMove = new();
        private string _eval = "0.0";

        [JsonInclude]
        public int Depth = 10;
        [JsonInclude]
        public bool AutoPerform;
        [JsonInclude]
        public int PlayAs = 1;
        [JsonInclude]
        public bool HideBestMove;
        [JsonInclude]
        public string Path = "";
        [JsonInclude]
        public int NthBestMove = 1;
        [JsonInclude]
        public bool ShowOtherMoves;
        [JsonInclude]
        public bool ShowEvalBar;
        [JsonInclude]
        public int PvCount = 1;
        [JsonInclude]
        public int Elo = 4000;
        [JsonInclude]
        public bool LimitElo;
        [JsonInclude]
        public int SkillLevel = 20;
        [JsonInclude]
        public bool UseSkillLevel;
        [JsonInclude]
        public int CheckDepth = 6;

        [JsonInclude]
        public string CurrentFen = "";

        [JsonInclude]
        public bool ShowConsole;

        [JsonIgnore]
        public List<EngineBridge.EngineType> EngineTypes = Enum.GetValues(typeof(EngineBridge.EngineType)).Cast<EngineBridge.EngineType>().ToList();

        [JsonIgnore] public List<string> EngineNames = new();

        [JsonIgnore] private string? _joinCode;
        [JsonIgnore] private string _textComposed = "";
        [JsonIgnore] private int _inputJoinCode = 0;
        [JsonIgnore] public List<(string, string)> ChatHistory = new();
        [JsonIgnore] public bool ScrollChatWindow = false;
        [JsonIgnore] public int MouseX = -1;
        [JsonIgnore] public int MouseY = -1;
        [JsonIgnore] public int TargetMouseX = -1;
        [JsonIgnore] public int TargetMouseY = -1;

        [JsonInclude]
        public int SelectedEngine;

        private long _frameCount;

        private List<Action> _postRenderList = new();

        [JsonInclude]
        public string CfgName = "A localChess user.";
        [JsonInclude]
        public bool CfgPrefersBlack = false;
        [JsonInclude]
        public string CfgIp = "10.0.0.1";
        [JsonInclude]
        public int CfgPort = 80;
        [JsonIgnore]
        public int MouseOpacity;
        [JsonInclude]
        public bool BroadcastMousePos;
        [JsonInclude]
        public string ProxyLogin = "";
        [JsonInclude]
        public bool UseProxyConfig = false;
        [JsonIgnore]
        public string InpProxyLogin = "";
        [JsonInclude]
        public bool UseJoinBroker = false;
        [JsonInclude]
        public string JoinBrokerAddress = "";
        [JsonIgnore]
        public int BrokerStage = 0;
        [JsonIgnore]
        public bool BrokerHasJoinCode = false;
        [JsonIgnore]
        public bool TestingBroker = false;
        [JsonIgnore]
        public int BrokerTestResult = 0;

        public void PostRender()
        {
            foreach (var cb in _postRenderList)
            {
                cb();
            }

            _postRenderList.Clear();
        }

        public void SaveToJson()
        {
            var json = JsonSerializer.Serialize(this);
            File.WriteAllText("C:\\localChess\\Settings.json", json);
        }

        public static IPAddress? GetLocalIPv4Address()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                string interfaceName = networkInterface.Name.ToLower();
                if (interfaceName.Contains("wi-fi") || interfaceName.Contains("ethernet") || interfaceName.Contains("lan"))
                {
                    foreach (UnicastIPAddressInformation ipInfo in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        // Filter out non-IPv4 addresses
                        if (ipInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            return ipInfo.Address;
                        }
                    }
                }
            }

            // Return null if no IPv4 address is found
            return null;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private int CountMoves(int depth, int maxDepth, Game selected)
        {
            if (depth == 0)
            {
                return 1;
            }

            var board = selected.Board;
            var numPositions = 0;

            for (var i = 0; i < selected.Board.Length; i++)
            {
                if (board[i] is null) continue;
                if (board[i]!.Black != selected.BlackPlaying) continue;

                if (ActiveGame!.EngineType == EngineBridge.EngineType.Alpaca)
                {
                    var intMoves = Engine.GetLegalMovesFor(i, selected, true, PieceType.Queen, selected.EnPassantIndex);
                    List<Move> moves = new();
                    foreach (var move in intMoves.moves)
                    {
                        if (board[i]!.Type == PieceType.Pawn && Game.GetPos(move).y is 0 or 7)
                        {
                            moves.Add(new Move(i, move)
                            {
                                PromoteInto = PieceType.Queen
                            });
                            moves.Add(new Move(i, move)
                            {
                                PromoteInto = PieceType.Rook
                            });
                            moves.Add(new Move(i, move)
                            {
                                PromoteInto = PieceType.Knight
                            });
                            moves.Add(new Move(i, move)
                            {
                                PromoteInto = PieceType.Bishop
                            });
                        }
                        else
                        {
                            moves.Add(new Move(i, move));
                        }
                    }

                    if (depth == maxDepth)
                    {
                        List<Thread> threads = new List<Thread>();
                        foreach (var move in moves)
                        {
                            var game = selected.Copy();
                            if (!game.PerformMove(move)) continue;

                            var thread = new Thread(() =>
                            {
                                var moveCount = CountMoves(depth - 1, maxDepth, game);
                                Interlocked.Add(ref numPositions, moveCount);
                                Console.WriteLine(move.ToUci() + @": " + moveCount);
                            })
                            {
                                IsBackground = true
                            };
                            thread.Start();
                            threads.Add(thread);
                        }

                        foreach (var thread in threads)
                        {
                            thread.Join();
                        }
                    }
                    else
                    {
                        foreach (var move in moves)
                        {
                            var game = selected.Copy();
                            if (game.PerformMove(move))
                            {
                                Interlocked.Add(ref numPositions, CountMoves(depth - 1, maxDepth, game));
                            }
                        }
                    }
                }
                else if(ActiveGame.EngineType == EngineBridge.EngineType.Bull)
                {
                    var moves = BullEngine.GetLegalMovesFor((ushort)i, selected.Board, selected.BlackPlaying, selected.EnPassantIndex, false);
                    var intMoves = moves.moves;
                    if (depth == maxDepth || true)
                    {
                        Parallel.ForEach(intMoves, new ParallelOptions()
                        {
                            MaxDegreeOfParallelism = 3
                        }, (mv) =>
                        {
                            var move = mv.Item1;
                            var game = selected.Copy();
                            EngineBridge.FastBullPerformMove(game, move, intMoves, false);
                            var moveCount = CountMoves(depth - 1, maxDepth, game);
                            Interlocked.Add(ref numPositions, moveCount);
                            if (depth == maxDepth)
                                Console.WriteLine(move.ToUci() + @": " + moveCount);
                        });
                    }
                    //else
                    //{
                    //    var cBoard = selected.Board.Select(a => a).ToArray();
                    //    var blackPlaying = selected.BlackPlaying;
                    //    var enPassantIndex = selected.EnPassantIndex;
                    //    var didJustEnPassant = selected.DidJustEnPassant;
                    //    foreach (var move in intMoves)
                    //    {
                    //        if (selected.PerformMove(move.Item1))
                    //        {
                    //            Interlocked.Add(ref numPositions, CountMoves(depth - 1, maxDepth, selected));
                    //        }
                    //
                    //        selected.Board = cBoard;
                    //        selected.BlackPlaying = blackPlaying;
                    //        selected.EnPassantIndex = enPassantIndex;
                    //        selected.DidJustEnPassant = didJustEnPassant;
                    //    }
                    //}
                }
                

            }

            return numPositions;
        }
        public static Gui LoadFromJson()
        {
            if (!File.Exists("C:\\localChess\\Settings.json"))
            {
                var gui = new Gui();
                var json = JsonSerializer.Serialize(gui);
                File.WriteAllText("C:\\localChess\\Settings.json", json);
                return gui;
            }

            var text = File.ReadAllText("C:\\localChess\\Settings.json");
            try
            {
                var gui = JsonSerializer.Deserialize<Gui>(text);
                return gui!;
            }
            catch (Exception)
            {
                // ignored
            }

            return new Gui();
        }

        public void Init()
        {
            EngineNames = EngineTypes.Select(e => e.ToString()).ToList();
            if (File.Exists("C:\\localChess\\stockfish_15.1_win_x64_avx2\\stockfish-windows-2022-x86-64-avx2.exe"))
            {
                Path = "C:\\localChess\\stockfish_15.1_win_x64_avx2\\stockfish-windows-2022-x86-64-avx2.exe";
            }

            WebProxy? proxy = null;
            if (ProxyLogin.Length != 0 && UseProxyConfig)
            {
                var parts = Base64Decode(ProxyLogin).Split("|");
                var (url, user, pass) = (parts[0], parts[1], parts[2]);
                proxy = new WebProxy
                {
                    Address = new Uri(url),
                    BypassProxyOnLocal = false,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                        userName: user,
                        password: pass)
                };
            }

            var handler = new HttpClientHandler
            {
                Proxy = proxy,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _client = new(handler: handler, disposeHandler: true);

            Program.ActiveGame!.OnMove += (_, move) =>
            {
                _moveList.Add(move.ToUci());
                EvaluateMove();
            };

        }

        private string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        private void EvaluateMove()
        {
            new Thread(() =>
            {
                _bestMove = new();
                var eval = UciEngine.Eval(ActiveGame!);
                if(eval > -500)
                    _eval = eval + "";
                
                _bestMove = UciEngine.GetBestMove(ActiveGame!, PvCount);

                }).Start();

            if (_moveList.Count > 10) return;
            var uciString = string.Join(",", _moveList);
            _opening = "...";
            
            new Thread(() =>
            {
                try
                {
                    HttpRequestMessage req = new(HttpMethod.Get, "https://explorer.lichess.ovh/lichess?play=" + uciString);
                    var res = _client!.SendAsync(req).Result.Content.ReadAsStringAsync().Result;
                    try
                    {
                        _opening = JsonSerializer.Deserialize<JsonObject>(res)!["opening"]?["name"]!.ToString();
                    }
                    catch (JsonException)
                    {
                        _opening = "Failed";
                    }
                }
                catch (Exception)
                {
                    _opening = "Request failed.";
                }
            }).Start();
        }

        public void OnFrame()
        {
            var flatBestMoves = new List<string>();
            foreach (var pv in _bestMove)
            {
                flatBestMoves.AddRange(pv);
            }

            var moveSelected = Math.Min(flatBestMoves.Count - 1, NthBestMove - 1);

            _frameCount++;
            if (BroadcastMousePos && _frameCount % 5 == 0 && Program.Network.Communication.IsConnected() && Program.Network.PlayingAgainst is not null)
            {
                Program.Network.Communication.SendPacket(new MousePosPacket
                    { X = Raylib.GetMouseX(), Y = Raylib.GetMouseY() });
            }

            if (TargetMouseX != -1 && TargetMouseY != -1 && Program.Network.Communication.IsConnected())
            {
                MouseX = (int)Lerp(MouseX, TargetMouseX, 0.18f);
                MouseY = (int)Lerp(MouseY, TargetMouseY, 0.18f);
                _postRenderList.Add(() =>
                {
                    Raylib.DrawCircle(MouseX, MouseY, 12,
                        new Color(255, 0, 0, MouseOpacity));
                });
                
            }
            if(MouseOpacity >= 2)
                MouseOpacity -= 2;

            if (_frameCount % 60 == 0)
            {
                _frameCount = 0;
                SaveToJson();
            }

            

            //ImGui.GetFont().Scale = 1.8f;
            if (ShowEvalBar)
            {
                ImGui.SetNextWindowPos(new Vector2(ActiveGame!.DisplaySize + 30, 0));
                ImGui.SetNextWindowSize(new Vector2(ActiveGame.DisplaySize - 30, ActiveGame.DisplaySize));
            }
            else
            {
                ImGui.SetNextWindowPos(new Vector2(ActiveGame!.DisplaySize, 0));
                ImGui.SetNextWindowSize(new Vector2(ActiveGame.DisplaySize, ActiveGame.DisplaySize));
            }
            
            //ImGui.GetStyle().Colors[ImGuiCol.table]
            if (flatBestMoves.Count > 0 && AutoPerform && ((ActiveGame.BlackPlaying ? 1 : 0) == PlayAs || PlayAs == 2))
            {
                try
                {
                    Move mv;
                    do
                    {
                        mv = Move.FromUci(flatBestMoves[moveSelected--]);
                    } while (!ActiveGame.PerformMove(mv) && moveSelected >= 0);

                    EvaluateMove();
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            if (ImGui.Begin("localChess", ref Opened,
                    ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.BeginGroup();
                ImGui.Text("Turn: " + (ActiveGame.BlackPlaying ? "Black" : "White"));
                ImGui.Text("Opening: ");
                ImGui.SameLine();
                ImGui.Text(_opening ?? "No known opening");

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Powered by Lichess");
                }

                if (FlagHashMismatch)
                {
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), "WARNING: ");
                    ImGui.SameLine();
                    ImGui.TextWrapped("A mismatch was detected in the version of localChess your opponent is using. They're either running an older/newer version or a modified one.");
                }

                ImGui.EndGroup();
                ImGui.GetBackgroundDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 1)));
                ImGui.BeginGroup();
                if (!Program.Network.Communication.IsConnected() && ImGui.Button("Reset game"))
                {
                    SaveToJson();
                    Program.Reset();
                }

                //if (_undoHistory.Count > 0)
                //{
                //    ImGui.SameLine();
                //    if (ImGui.Button("Undo move"))
                //    {
                //        var game = _undoHistory[^1];
                //        Program.ActiveGame = game;
                //        ActiveGame = game;
                //        _undoHistory.RemoveAt(_undoHistory.Count - 1);
                //    }
                //}
                
                ImGui.EndGroup();
                ImGui.GetBackgroundDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 1)));
                ImGui.Separator();

                if (ImGui.CollapsingHeader("Internal engine"))
                {
                    ImGui.ListBox("Internal engine", ref SelectedEngine, EngineNames.ToArray(), EngineNames.Count);
                    ImGui.TextWrapped("Engine description: " + EngineBridge.GetDescription(ActiveGame.EngineType));
                    ImGui.Text("Last engine time: " + ActiveGame.LastElapsedTicks + " ticks.");
                }
                ActiveGame.EngineType = EngineTypes[SelectedEngine];
                if (ImGui.CollapsingHeader("Stockfish", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var bestMove = "";
                    if (moveSelected < 0)
                    {
                        moveSelected = 0;
                    }

                    if (flatBestMoves.Count > 0)
                    {
                        bestMove = flatBestMoves[Math.Min(moveSelected, flatBestMoves.Count - 1)];
                    }
                    ImGui.Text("Next best move: " + (HideBestMove ? "[hidden]" : bestMove));
                    ImGui.SameLine();
                    if (flatBestMoves.Count > 0)
                    {
                        if (!Program.Network.Communication.IsConnected() && ImGui.Button("Perform"))
                        {
                            try
                            {
                                Move mv;
                                do
                                {
                                    mv = Move.FromUci(flatBestMoves[moveSelected--]);
                                } while (!ActiveGame.PerformMove(mv) && moveSelected >= 0);
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                        }
                    } else if (UciEngine.StockfishProcess != null)
                    {
                        if (ImGui.Button("Kill"))
                        {
                            UciEngine.StockfishProcess.Kill();
                            UciEngine.StockfishProcess = null;
                        }
                            
                    }
                    else
                    {
                        ImGui.Text("(not active)");
                    }
                    ImGui.Text("Evaluation (white): " + _eval);
                    ImGui.SameLine();
                    ImGui.Checkbox("Show bar", ref ShowEvalBar);
                    ImGui.SameLine();
                    ImGui.Checkbox("Show move PV list", ref ShowOtherMoves);
                    if (ShowOtherMoves && ImGui.BeginChild("movepvc", new Vector2(ActiveGame.DisplaySize, 300)))
                    {
                        if (ImGui.BeginTable("movepvs", 3))
                        {
                            ImGui.TableHeadersRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text("PV #");
                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text("Selection #");
                            ImGui.TableSetColumnIndex(2);
                            ImGui.Text("UCI");

                            var pvCount = 0;
                            foreach (var pv in _bestMove)
                            {
                                pvCount++;
                                var uciCount = 0;
                                foreach (var uci in pv)
                                {
                                    ImGui.TableNextRow();
                                    uciCount++;

                                    ImGui.TableSetColumnIndex(0);
                                    ImGui.Text(pvCount + "");
                                    ImGui.TableSetColumnIndex(1);
                                    ImGui.Text(uciCount + "");
                                    ImGui.TableSetColumnIndex(2);
                                    if (ImGui.Button(uci))
                                    {
                                        try
                                        {
                                            var mv = Move.FromUci(uci);
                                            ActiveGame.PerformMove(mv);
                                        }
                                        catch (Exception)
                                        {
                                            // ignored
                                        }
                                    }
                                }
                            }
                            ImGui.EndTable();
                        }
                        ImGui.EndChild();
                    }

                    ImGui.SliderInt("Depth", ref Depth, 1, 100);
                    ImGui.SliderInt("PV Count", ref PvCount, 1, 15);
                    ImGui.SliderInt("Estimated ELO", ref Elo, 1, 4000);
                    ImGui.SameLine();
                    ImGui.Checkbox("Limit ELO", ref LimitElo);
                    ImGui.SliderInt("Skill Level", ref SkillLevel, 0, 20);
                    ImGui.SameLine();
                    ImGui.Checkbox("Use Skill Level", ref UseSkillLevel);
                    if (UseSkillLevel)
                    {
                        ImGui.Text("Est. ELO: " + (1000 + SkillLevel * 120));
                    }

                    UciEngine.SkillLevel = SkillLevel;
                    UciEngine.UseSkillLevel = UseSkillLevel;
                    UciEngine.Elo = Elo;
                    UciEngine.LimitElo = LimitElo;

                    ImGui.SliderInt("Play n'th move", ref NthBestMove, 1, Depth * PvCount);
                    ImGui.Checkbox("Hide best-move", ref HideBestMove);
                    if (Program.Network.Communication.IsConnected())
                    {
                        HideBestMove = true;
                    }
                    ImGui.SameLine();
                    ImGui.Checkbox("Auto-perform", ref AutoPerform);
                    if (Program.Network.Communication.IsConnected())
                    {
                        AutoPerform = false;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Force re-evaluate", new Vector2(ImGui.GetContentRegionAvail().X, 18)))
                    {
                        EvaluateMove();
                    }
                    ImGui.ListBox("Auto-perform as", ref PlayAs, new List<string> { "White", "Black", "Both" }.ToArray(), 3);
                    UciEngine.Depth = Depth;
                    ImGui.InputText("Stockfish path", ref Path, 400);
                    UciEngine.Path = Path;
                    
                    if (ImGui.Button("Download", new Vector2(ImGui.GetContentRegionAvail().X, 18)))
                    {
                        try
                        {
                            var zip = _client!.GetByteArrayAsync("https://stockfishchess.org/files/stockfish_15.1_win_x64_avx2.zip").Result;
                            File.WriteAllBytes("C:\\localChess\\stockfish.zip", zip);
                            ZipFile.ExtractToDirectory("C:\\localChess\\stockfish.zip", "C:\\localChess\\");
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        Path = "C:\\localChess\\stockfish_15.1_win_x64_avx2\\stockfish-windows-2022-x86-64-avx2.exe";
                    }
                }

                if (ImGui.CollapsingHeader("Networking"))
                {
                    if (!Program.Network.Communication.IsConnected() && BrokerStage == 0)
                    {
                        ImGui.InputText("Username", ref CfgName, 64);
                        Program.Network.Name = CfgName;
                        ImGui.SameLine();
                        ImGui.Checkbox("Play as black", ref CfgPrefersBlack);
                        ImGui.Separator();
                        ImGui.Text("Easy networking");
                        ImGui.Separator();
                        if (ImGui.Button("Generate join code"))
                        {
                            if (!UseJoinBroker)
                            {
                                var ip = GetLocalIPv4Address()!.ToString().Split(".");
                                var code = ip[3].PadLeft(3, '0') + ip[2];
                                _joinCode = code;
                                Program.StartServer(80);
                            }
                            else
                            {
                                BrokerHasJoinCode = false;
                                BrokerStage = 1;
                            }
                            
                        }

                        ImGui.InputInt("Join code", ref _inputJoinCode);
                        ImGui.SameLine();
                        if (ImGui.Button("Join"))
                        {
                            if (!UseJoinBroker)
                            {
                                var ip = GetLocalIPv4Address()!.ToString().Split(".");
                                var beginIp = ip[0] + "." + ip[1] + "." + _inputJoinCode.ToString()[3..] + "." + _inputJoinCode.ToString()[..3].TrimStart('0');
                                Console.WriteLine(beginIp);
                                Program.Connect(beginIp, 80);
                            }
                            else
                            {
                                BrokerHasJoinCode = true;
                                BrokerStage = 1;
                            }
                            
                        }
                        ImGui.Separator();
                        ImGui.Text("Advanced networking");
                        ImGui.Separator();
                        Program.Network.PrefersBlack = CfgPrefersBlack;
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Option only takes effect when hosting server");
                        }

                        ImGui.InputText("Server address", ref CfgIp, 64);
                        ImGui.InputInt("Port", ref CfgPort);

                        if (ImGui.Button("Connect"))
                        {
                            Program.Connect(CfgIp, CfgPort);
                        }

                        ImGui.SameLine();
                        if (ImGui.Button("Start server"))
                        {
                            Program.StartServer(CfgPort);
                        }
                        ImGui.Separator();
                        ImGui.Text("Join broker");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(
                                "A join broker is needed for join codes to work when connecting peers in different networks.");
                        }
                        ImGui.Separator();
                        ImGui.InputText("Broker host", ref JoinBrokerAddress, 64);
                        ImGui.SameLine();
                        ImGui.Checkbox("Use broker", ref UseJoinBroker);
                        if (!TestingBroker && ImGui.Button("Test broker"))
                        {
                            TestingBroker = true;
                            BrokerTestResult = 0;
                            new Thread(() =>
                            {
                                try
                                {
                                    HttpRequestMessage req = new(HttpMethod.Get, $"http://{JoinBrokerAddress}/ping");
                                    var res = _client!.SendAsync(req).Result.Content.ReadAsStringAsync().Result;
                                    var body = JsonSerializer.Deserialize<JsonObject>(res)!["body"]!.ToString();
                                    if (body == "localChess broker")
                                    {
                                        BrokerTestResult = 2;
                                    }
                                }
                                catch (Exception)
                                {
                                    BrokerTestResult = 1;
                                }
                                TestingBroker = false;
                            }).Start();
                        }

                        if(!TestingBroker)
                            ImGui.SameLine();

                        if (BrokerTestResult != 0)
                        {
                            switch (BrokerTestResult)
                            {
                                case 1:
                                    ImGui.Text("Failed -- invalid broker");
                                    break;
                                case 2:
                                    ImGui.Text("Passed -- valid broker");
                                    break;
                            }
                        } else if (BrokerTestResult == 0 && TestingBroker)
                        {
                            ImGui.Text("Testing...");
                        }

                        ImGui.Separator();
                        ImGui.Text("Proxy configuration");
                        ImGui.Separator();
                        ImGui.InputText("Proxy configuration", ref InpProxyLogin, 64);
                        ImGui.SameLine();
                        if (ImGui.Button("Save"))
                        {
                            ProxyLogin = Base64Encode(InpProxyLogin);
                        }
                        ImGui.Text("Format: PROXY_URL|PROXY_USER|PROXY_PASS");
                        ImGui.Checkbox("Use proxy configuration", ref UseProxyConfig);

                    }
                    else if (BrokerStage != 0)
                    {
                        ImGui.Separator();
                        ImGui.Text("Join broker");
                        ImGui.Separator();
                        if (BrokerStage == 1 && !BrokerHasJoinCode)
                        {
                            BrokerStage = 2;
                            Program.Network.Communication.Stop();
                            new Thread(() =>
                            {
                                HttpRequestMessage req = new(HttpMethod.Get, $"http://{JoinBrokerAddress}/generate");
                                var res = _client!.SendAsync(req).Result.Content.ReadAsStringAsync().Result;
                                try
                                {
                                    _joinCode = JsonSerializer.Deserialize<JsonObject>(res)!["code"]?.ToString();
                                    Console.WriteLine("got code: " + _joinCode);
                                    if (!PortForward())
                                    {
                                        Console.WriteLine("Port forward failed");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Created uPnP port forward");
                                    }
                                    BrokerStage = 3;
                                }
                                catch (JsonException)
                                {
                                    Program.Network.Communication.Stop();
                                    BrokerStage = 0;
                                }
                            }).Start();
                        }
                        else if(BrokerStage == 1)
                        {
                            _joinCode = _inputJoinCode.ToString();
                            BrokerStage = 3;
                        }

                        if (BrokerStage is >= 1 and <= 2)
                        {
                            ImGui.Text("Port forwarding & generating join code...");
                        }

                        string peer = "";
                        if (BrokerStage == 3)
                        {
                            BrokerStage = 4;
                            new Thread(() =>
                            {
                                while (BrokerStage == 4)
                                {
                                    HttpRequestMessage req = new(HttpMethod.Get, $"http://{JoinBrokerAddress}/join?code=" + _joinCode + "&server=" + (!BrokerHasJoinCode).ToString().ToLower());
                                    var pRes = _client!.SendAsync(req).Result;
                                    var res = pRes.Content.ReadAsStringAsync().Result;
                                    if (pRes.StatusCode == HttpStatusCode.NotFound)
                                    {
                                        BrokerStage = 0;
                                        Console.WriteLine("Invalid join code");
                                    }
                                    try
                                    {
                                        var jpeer = JsonSerializer.Deserialize<JsonObject>(res)!["peer"];
                                        if (jpeer is null)
                                        {
                                            Thread.Sleep(150);
                                            continue;
                                        }

                                        peer = jpeer.ToString();
                                        Console.WriteLine("Got peer: " + peer);
                                        
                                        if (!BrokerHasJoinCode)
                                        {
                                            Program.StartServer(9191);
                                        }
                                        else
                                        {
                                            Program.Connect(peer, 9191);
                                        }
                                        BrokerStage = 5;
                                        break;
                                    }
                                    catch (JsonException)
                                    {
                                        Program.Network.Communication.Stop();
                                        BrokerStage = 0;
                                        break;
                                    }
                                }
                            }).Start();
                        }

                        if (BrokerStage is >= 3 and <= 4)
                        {
                            ImGui.Text($"Join code: {_joinCode}. Waiting...");
                        }

                        if (BrokerStage == 5)
                        {
                            BrokerStage = 0;
                        }

                        if (ImGui.Button("Stop"))
                        {
                            BrokerStage = 0;
                            Program.Network.Communication.Stop();
                        }

                        ImGui.SameLine();
                        if (_joinCode is not null && _joinCode.Length != 0 && ImGui.Button("Copy to clipboard"))
                        {
                            Raylib.SetClipboardText(_joinCode);
                        }
                    }
                    else
                    {
                        if (Program.Network.PlayingAgainst is not null)
                        {
                            AutoPerform = false;
                            ImGui.Text("Playing against: '" + Program.Network.PlayingAgainst + "'");
                            ImGui.Text("Playing as: " + (ActiveGame.LockedColour!.Value ? "black" : "white"));
                            ImGui.Checkbox("Broadcast mouse position", ref BroadcastMousePos);
                            _joinCode = null;
                            ImGui.BeginGroup();
                            ImGui.Text("Chat");
                            ImGui.Separator();
                            if (ImGui.BeginChild("chatwindow", new Vector2(ImGui.GetContentRegionAvail().X, 200)))
                            {
                                foreach (var (username, message) in ChatHistory)
                                {
                                    ImGui.TextWrapped(username + " : ");
                                    ImGui.SameLine();
                                    ImGui.TextWrapped(message);
                                    ImGui.Separator();
                                }
                                if (ScrollChatWindow)
                                {
                                    ImGui.SetScrollHereY(1.0f);
                                    ScrollChatWindow = false;
                                }
                                ImGui.EndChild();
                            }

                            if (Program.Network.SentKeys && Program.Network.PeerPublicKey is not null)
                            {
                                ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "INFO: ");
                                ImGui.SameLine();
                                ImGui.Text("Network traffic is end-to-end encrypted, messages cannot be read by 3rd parties");
                            }
                            else
                            {
                                ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "WARNING: ");
                                ImGui.SameLine();
                                ImGui.Text("Messages are unencrypted and easily observable");
                            }
                            
                            var enterPressed = ImGui.InputText("##chatinput", ref _textComposed, 256, ImGuiInputTextFlags.EnterReturnsTrue);
                            if (enterPressed)
                            {
                                ImGui.SetKeyboardFocusHere(-1);
                            }

                            ImGui.SameLine();
                            if (ImGui.Button("Send", new Vector2(ImGui.GetContentRegionAvail().X, 18)) || enterPressed && _textComposed.Trim().Length != 0)
                            {
                                var composed = _textComposed.Trim();
                                _textComposed = "";

                                ChatHistory.Add((Program.Network.Name, composed));
                                Program.Network.Communication.SendPacket(new ChatPacket { Content = composed });
                                ScrollChatWindow = true;
                            }
                            ImGui.EndGroup();
                            ImGui.GetForegroundDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.8f, 0.8f, 1)));
                        }
                        else
                        {
                            var ip = GetLocalIPv4Address();
                            if (ip is not null)
                            {
                                ImGui.Text("Your local IPv4 address is: " + ip);
                            }
                        }

                        if (_joinCode is not null)
                        {
                            ImGui.Text("Join code: " + _joinCode);
                        }
                        if (ImGui.Button("Disconnect/end server"))
                        {
                            Program.Network.Communication.Stop();
                        }
                    }
                    
                }
                if (!Program.Network.Communication.IsConnected() && ImGui.CollapsingHeader("FEN loader"))
                {
                    ImGui.InputText("FEN string", ref CurrentFen, 128);
                    ImGui.SameLine();
                    if (ImGui.Button("Load", new Vector2(ImGui.GetContentRegionAvail().X, 18)))
                    {
                        var game = Game.FromFen(CurrentFen);
                        Program.ActiveGame = game;
                        ActiveGame = game;
                    }

                    ImGui.Text("Current FEN: " + ActiveGame!.GetFen());
                    ImGui.SameLine();
                    if (ImGui.Button("Copy to clipboard", new Vector2(ImGui.GetContentRegionAvail().X, 18)))
                    {
                        Raylib.SetClipboardText(ActiveGame.GetFen());
                    }

                    ImGui.Separator();
                    ImGui.Text("FEN presets");

                    void Preset(string name, string fen)
                    {
                        if (ImGui.Button(name, new Vector2(ImGui.GetContentRegionAvail().X, 25)))
                        {
                            var game = Game.FromFen(fen);
                            Program.ActiveGame = game;
                            ActiveGame = game;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            _postRenderList.Add(() =>
                            {
                                var mPos = Raylib.GetMousePosition();
                                var game = Game.FromFen(fen);
                                game.DisplaySize = 360;
                                if(mPos.Y > 360)
                                    game.Render((int)mPos.X, (int)mPos.Y - game.DisplaySize);
                                else
                                    game.Render((int)mPos.X, (int)mPos.Y);
                            });
                        }
                    }

                    var oldItemSpacing = ImGui.GetStyle().ItemSpacing;

                    ImGui.GetStyle().ItemSpacing = new Vector2(0, 0);
                    Preset("Ral's Trapped Queens Variant", "r1b1kb1r/Pp1ppppp/qP6/Pp6/pP6/Qp6/pP1PPPPP/R1B1KB1R w KQkq - 0 1");
                    Preset("Promotion Prevention", "5bnr/5ppp/5pkp/5ppp/PPP5/PKP5/PPP5/RNB5 w - - 0 1");
                    Preset("Ral's Trapped Kings Variant", "r2q1nnr/Pp1ppppp/kP6/Pp6/pP6/Kp6/pP1PPPPP/R2Q1NNR w - - 0 1");
                    Preset("Ral's Lunar Eclipse Position", "k3r2r/pnbpPb2/1pp2Pp1/1p4P1/1p4P1/1Pp2PP1/2BpPBNP/R2R3K w - - 0 1");
                    Preset("Danger", "2bb1bb1/2ppp3/8/3p4/B1pkp1B1/2RpR3/1p1P1p2/1N1K1N2 w - - 0 1");
                    Preset("Miserable Kings", "1kr1qbnr/3bpppp/2np4/8/8/4PN2/PPPPB3/RNBQ1RK1 w - - 0 1");
                    Preset("Locked", "6nk/3p2pr/p2Pp1pb/p2pP1p1/Pp1Pp1P1/1P1pP3/NR1P4/KNB5 w - - 0 1");
                    Preset("Triple Knight Defense", "rqbnnn1k/4pppp/8/8/8/8/PPPP4/K1NNNBRQ w - - 0 1");
                    Preset("Trapped Queens and Kings Combined", "r2n3r/Pp1pp1p1/qP4Pk/Pp4pP/pP4Pp/Qp4pK/pP1PP1P1/R2N3R w - - 0 1");
                    Preset("Pawns of Perfection", "rnnqkbbr/4pppp/8/4PPPP/pppp4/8/PPPP4/RBBQKNNR w - - 0 1");
                    Preset("Ultimate Inbalances", "kbrrqrbr/pppnnnpp/3ppp2/8/8/1P4P1/PQPPPPQP/RNBBKBBR w KQ - 0 1");
                    Preset("What's Your Decision?", "nbrnbrbq/pppp1p1p/ppp2PpP/k5P1/1p5K/pPp2PPP/P1P1PPPP/QBRBNRBN w - - 0 1");
                    Preset("Cat and Mouse", "b1b1b1bk/ppp1P1pp/4P3/4P3/4P3/4P3/P3P2P/4KBNR w - - 0 1");
                    Preset("Super KID", "r1bqnrkr/pppbnnbp/3p1qp1/3Pp3/2P1Pp2/B1NN1P2/PPRQBBPP/R2QNRK1 w - - 0 13");
                    Preset("Overly Ambitious", "k1nqn3/p1p1p1p1/PpPpPpPp/1P1P1P1P/8/8/6NN/6NK w - - 0 1");
                    ImGui.GetStyle().ItemSpacing = oldItemSpacing;

                    ImGui.Separator();
                }
                if (!Program.Network.Communication.IsConnected() && ImGui.CollapsingHeader("Move checker / engine tester"))
                {
                    ImGui.SliderInt("Check depth", ref CheckDepth, 0, 20);
                    if (ImGui.Button("Check"))
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        var count = CountMoves(CheckDepth, CheckDepth, ActiveGame.Copy());
                        sw.Stop();
                        Console.WriteLine(
                            $@"Depth: {CheckDepth}, Nodes: {count}. Nodes/sec: {count / (sw.ElapsedMilliseconds / 1000.0f)}. {(sw.ElapsedMilliseconds / 1000.0f)} sec.");

                    }
                }
                if (ImGui.CollapsingHeader("Move history"))
                {
                    if (ImGui.BeginTable("movehistory", 3))
                    {
                        ImGui.TableHeadersRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text("Move #");
                        ImGui.TableSetColumnIndex(1);

                        ImGui.Text("Colour");
                        ImGui.TableSetColumnIndex(2);

                        ImGui.Text("UCI");
                        bool white = true;
                        int count = 1;
                        foreach (var move in _moveList)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text(count + "");
                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text(white ? "White" : "Black");
                            ImGui.TableSetColumnIndex(2);
                            ImGui.Text(move);
                            white = !white;
                            count++;
                        }
                    }
                    ImGui.EndTable();
                }

            }
            ImGui.PopStyleColor();

            if (ImGui.Checkbox("Show console", ref ShowConsole))
            {
                Console.Clear();
                Program.ShowWindow(Program.GetConsoleWindow(), ShowConsole ? Program.SwShow : Program.SwHide);
                Console.WriteLine(@"localChess -- made by jayphen");
            }

            if (ShowEvalBar)
            {
                ImGui.GetBackgroundDrawList().AddRectFilled(new Vector2(ActiveGame.DisplaySize, 0), new Vector2(ActiveGame.DisplaySize + 30, ActiveGame.DisplaySize),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.05f, 1.0f)));
                _currentEval = Lerp(_currentEval, float.Parse(_eval), 0.2f);
                ImGui.GetBackgroundDrawList().AddRectFilled(new Vector2(ActiveGame.DisplaySize, ActiveGame.DisplaySize),
                    new Vector2(ActiveGame.DisplaySize + 30,
                        ActiveGame.DisplaySize / 2.0f - (ActiveGame.DisplaySize / 2.0f * _currentEval)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.9f, 0.9f, 1.0f)));
            }

            ImGui.End();
            if (ActiveGame.GetMouseBoardPosition().HasValue && ActiveGame.SelectedIndex is not null && ActiveGame.LegalMoves is not null)
            {
                if (ActiveGame.Board[ActiveGame.SelectedIndex.Value] is null) return;
                if (ActiveGame.Board[ActiveGame.SelectedIndex.Value]!.Type != PieceType.Pawn) return;
                var mousePos = ActiveGame.GetMouseBoardPosition();
                var mousePosIndex = Game.GetIndex(mousePos!.Value.x, mousePos.Value.y);
                foreach (var move in ActiveGame.LegalMoves!)
                {
                    var (moveX, moveY) = Game.GetPos(move);
                    if (moveY is not (0 or 7) || mousePosIndex != move) continue;
                    ImGui.SetNextWindowPos(new Vector2(moveX * 90, moveY * 90));
                    ImGui.SetNextWindowSize(new Vector2(90, 90));
                    var oldItemSpacing = ImGui.GetStyle().ItemSpacing;
                    var oldWindowPadding = ImGui.GetStyle().WindowPadding;

                    ImGui.GetStyle().ItemSpacing = new Vector2(0, 0);
                    ImGui.GetStyle().WindowPadding = new Vector2(0, 0);
                    if (ImGui.Begin("Pawn promotion", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
                    {
                        
                        if (ImGui.Button("Queen"))
                        {

                        }

                        if (ImGui.IsItemHovered())
                        {
                            Move.DefaultPromoteInto = PieceType.Queen;
                        }

                        if (ImGui.Button("Knight"))
                        {

                        }

                        if (ImGui.IsItemHovered())
                        {
                            Move.DefaultPromoteInto = PieceType.Knight;
                        }

                        if (ImGui.Button("Bishop"))
                        {

                        }

                        if (ImGui.IsItemHovered())
                        {
                            Move.DefaultPromoteInto = PieceType.Bishop;
                        }

                        if (ImGui.Button("Rook"))
                        {

                        }

                        if (ImGui.IsItemHovered())
                        {
                            Move.DefaultPromoteInto = PieceType.Rook;
                        }

                        if (!ImGui.IsAnyItemHovered())
                        {
                            Move.DefaultPromoteInto = PieceType.Queen;
                        }
                    }
                    ImGui.End();
                    ImGui.GetStyle().ItemSpacing = oldItemSpacing;
                    ImGui.GetStyle().WindowPadding = oldWindowPadding;
                }
            }

        }

        private static bool PortForwarded = false;
        public static bool PortForward()
        {
            if (PortForwarded == true)
            {
                return true;
            }

            int portToForward = 9191;

            bool portMapSet = false;
            bool fail = false;

            NatUtility.DeviceFound += (object sender, DeviceEventArgs args) =>
            {
                try
                {
                    INatDevice device = args.Device;
                    device.CreatePortMap(new Mapping(Protocol.Tcp, portToForward, portToForward, 0, "localChess"));
                    portMapSet = true;

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    fail = true;
                }
            };

            NatUtility.StartDiscovery();

            while (true)
            {
                if (portMapSet || fail) break;
                Thread.Sleep(1);
            }
            PortForwarded = !fail;
            return !fail;
        }
    }
}
