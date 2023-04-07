using System.IO.Compression;
using System.Net;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ImGuiNET;
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

        private string _opening = "...";
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

                if (ActiveGame!.EngineType == EngineBridge.EngineType.Bull)
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
                else
                {
                    var intMoves = BullEngine.GetLegalMovesFor((ushort)i, selected.Board, true, selected.EnPassantIndex).moves;
                    if (depth == maxDepth)
                    {
                        var threads = new List<Thread>();
                        foreach (var mv in intMoves)
                        {
                            var move = mv.Item1;
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
                        foreach (var move in intMoves)
                        {
                            var game = selected.Copy();
                            if (game.PerformMove(move.Item1))
                            {
                                Interlocked.Add(ref numPositions, CountMoves(depth - 1, maxDepth, game));
                            }
                        }
                    }
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

            var handler = new HttpClientHandler();
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
            _client = new(handler);

            ActiveGame!.OnMove += (_, move) =>
            {
                _moveList.Add(move.ToUci());
                EvaluateMove();
            };

        }

        private void EvaluateMove()
        {
            new Thread(() =>
            {
                _bestMove = new();
                _eval = UCIEngine.Eval(ActiveGame!) + "";
                _bestMove = UCIEngine.GetBestMove(ActiveGame!, PvCount);
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
                        _opening = JsonSerializer.Deserialize<JsonObject>(res)!["opening"]!["name"]!.ToString();
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
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Powered by Lichess");
                }

                ImGui.SameLine();
                ImGui.Text(_opening);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Powered by Lichess");
                }

                ImGui.EndGroup();
                ImGui.GetBackgroundDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 1)));
                ImGui.Separator();
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
                    ActiveGame.EngineType = EngineTypes[SelectedEngine];
                    ImGui.TextWrapped("Engine description: " + EngineBridge.GetDescription(ActiveGame.EngineType));
                    ImGui.Text("Last engine time: " + ActiveGame.LastElapsedTicks + " ticks.");
                }

                ImGui.Separator();
                if (!Program.Network.Communication.IsConnected() && ImGui.CollapsingHeader("Stockfish", ImGuiTreeNodeFlags.DefaultOpen))
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
                        if (ImGui.Button("Perform"))
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
                    } else if (UCIEngine.StockfishProcess != null)
                    {
                        if (ImGui.Button("Kill"))
                        {
                            UCIEngine.StockfishProcess.Kill();
                            UCIEngine.StockfishProcess = null;
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

                    UCIEngine.SkillLevel = SkillLevel;
                    UCIEngine.UseSkillLevel = UseSkillLevel;
                    UCIEngine.Elo = Elo;
                    UCIEngine.LimitElo = LimitElo;

                    ImGui.SliderInt("Play n'th move", ref NthBestMove, 1, Depth * PvCount);
                    ImGui.Checkbox("Hide best-move", ref HideBestMove);
                    ImGui.SameLine();
                    ImGui.Checkbox("Auto-perform", ref AutoPerform);
                    ImGui.SameLine();
                    if (ImGui.Button("Force re-evaluate", new Vector2(ImGui.GetContentRegionAvail().X, 18)))
                    {
                        EvaluateMove();
                    }
                    ImGui.ListBox("Auto-perform as", ref PlayAs, new List<string> { "White", "Black", "Both" }.ToArray(), 3);
                    UCIEngine.Depth = Depth;
                    ImGui.InputText("Stockfish path", ref Path, 400);
                    UCIEngine.Path = Path;
                    
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
                ImGui.Separator();
                if (ImGui.CollapsingHeader("Networking"))
                {
                    if (!Program.Network.Communication.IsConnected())
                    {
                        ImGui.InputText("Username", ref CfgName, 64);
                        Program.Network.Name = CfgName;
                        ImGui.SameLine();
                        ImGui.Checkbox("Play as black", ref CfgPrefersBlack);
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
                    }
                    else
                    {
                        if (Program.Network.PlayingAgainst is not null)
                        {
                            ImGui.Text("Playing against: '" + Program.Network.PlayingAgainst + "'");
                            ImGui.Text("Playing as: " + (ActiveGame.LockedColour!.Value ? "black" : "white"));
                        }
                        if (ImGui.Button("Disconnect/end server"))
                        {
                            Program.Network.Communication.Stop();
                        }
                    }
                    
                }
                ImGui.Separator();
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
                
                ImGui.Separator();
                if (ImGui.CollapsingHeader("Move checker / engine tester"))
                {
                    ImGui.SliderInt("Check depth", ref CheckDepth, 0, 20);
                    if (ImGui.Button("Check"))
                    {
                        Console.WriteLine(@"Depth: " + CheckDepth + @", Moves: " + CountMoves(CheckDepth, CheckDepth, ActiveGame.Copy()));
                    }
                }
                
                ImGui.Separator();

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
                Program.ShowWindow(Program.GetConsoleWindow(), ShowConsole ? Program.SW_SHOW : Program.SW_HIDE);
                Console.WriteLine(@"localChess -- made by jayphen");
            }

            if (ShowEvalBar)
            {
                ImGui.GetBackgroundDrawList().AddRectFilled(new Vector2(ActiveGame.DisplaySize, 0), new Vector2(ActiveGame.DisplaySize + 30, ActiveGame.DisplaySize),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.05f, 1.0f)));
                float Lerp(float a, float b, float t)
                {
                    return a + (b - a) * t;
                }

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
    }
}
