using ImGuiNET;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace localChess.Chess
{
    internal class GUI
    {
        [JsonIgnore]
        public Game ActiveGame { get; set; }

        [JsonIgnore] 
        private int _lastDepth = 0;
        [JsonIgnore]
        private int _lastPV = 0;

        [JsonInclude]
        public bool Opened = true;
        [JsonInclude]
        public bool GameInfoOpened = true;

        [JsonIgnore]
        private float _currentEval = 0.0f;

        private string _opening = "...";
        private HttpClient _client;
        private readonly List<string> _moveList = new();
        private readonly List<Game> _undoHistory = new();
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
        public int PVCount = 1;
        [JsonInclude]
        public int ELO = 4000;
        [JsonInclude]
        public bool LimitElo = false;
        [JsonInclude]
        public int SkillLevel = 20;
        [JsonInclude]
        public bool UseSkillLevel = false;
        [JsonInclude]
        public int CheckDepth = 6;
        [JsonInclude]
        public string CurrentFen = "";

        private long _frameCount = 0;

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
                if (board[i].Black != selected.BlackPlaying) continue;

                if (!Game.UseBullEngine)
                {
                    var intMoves = Engine.GetLegalMovesFor(i, selected, true, PieceType.Queen, selected.EnPassantIndex);
                    List<Move> moves = new();
                    foreach (var move in intMoves.moves)
                    {
                        if (board[i].Type == PieceType.Pawn && Game.GetPos(move).y is 0 or 7)
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
                                numPositions += moveCount;
                                Console.WriteLine(move.ToUCI() + ": " + moveCount);
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
                                numPositions += CountMoves(depth - 1, maxDepth, game);
                            }
                        }
                    }
                }
                else
                {
                    var intMoves = BullEngine.GetLegalMovesFor((ushort)i, selected.Board, true, selected.EnPassantIndex).moves;
                    if (depth == maxDepth)
                    {
                        List<Thread> threads = new List<Thread>();
                        foreach (var mv in intMoves)
                        {
                            var move = mv.Item1;
                            var game = selected.Copy();
                            if (!game.PerformMove(move)) continue;

                            var thread = new Thread(() =>
                            {
                                var moveCount = CountMoves(depth - 1, maxDepth, game);
                                numPositions += moveCount;
                                Console.WriteLine(move.ToUCI() + ": " + moveCount);
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
                                numPositions += CountMoves(depth - 1, maxDepth, game);
                            }
                        }
                    }
                }
                

            }

            return numPositions;
        }
        public static GUI LoadFromJson()
        {
            if (!File.Exists("C:\\localChess\\Settings.json"))
            {
                var gui = new GUI();
                var json = JsonSerializer.Serialize(gui);
                File.WriteAllText("C:\\localChess\\Settings.json", json);
                return gui;
            }
            else
            {
                var text = File.ReadAllText("C:\\localChess\\Settings.json");
                try
                {
                    var gui = JsonSerializer.Deserialize<GUI>(text);
                    return gui!;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return new GUI();
        }

        public void Init()
        {
            if (File.Exists("C:\\localChess\\stockfish_15.1_win_x64_avx2\\stockfish-windows-2022-x86-64-avx2.exe"))
            {
                Path = "C:\\localChess\\stockfish_15.1_win_x64_avx2\\stockfish-windows-2022-x86-64-avx2.exe";
            }

            var handler = new HttpClientHandler();
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
            _client = new(handler);

            ActiveGame.OnMove += (_, move) =>
            {
                _moveList.Add(move.ToUCI());
                _undoHistory.Add(Program.ActiveGame?.Copy());
                EvaluateMove();
            };

        }

        private void EvaluateMove()
        {
            new Thread(() =>
            {
                _bestMove = new();
                _eval = UCIEngine.Eval(_moveList) + "";
                _lastDepth = UCIEngine.Depth;
                _lastPV = PVCount;
                _bestMove = UCIEngine.GetBestMove(_moveList, PVCount);
            }).Start();

            if (_moveList.Count > 10) return;
            var uciString = string.Join(",", _moveList);
            _opening = "...";
            new Thread(() =>
            {
                try
                {
                    HttpRequestMessage req = new(HttpMethod.Get, "https://explorer.lichess.ovh/lichess?play=" + uciString);
                    var res = _client.SendAsync(req).Result.Content.ReadAsStringAsync().Result;
                    try
                    {
                        _opening = JsonSerializer.Deserialize<JsonObject>(res)["opening"]["name"].ToString();
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
                ImGui.SetNextWindowPos(new Vector2(750, 0));
                ImGui.SetNextWindowSize(new Vector2(720 - (750 - 720), 720));
            }
            else
            {
                ImGui.SetNextWindowPos(new Vector2(720, 0));
                ImGui.SetNextWindowSize(new Vector2(720, 720));
            }
            
            //ImGui.GetStyle().Colors[ImGuiCol.table]
            if (flatBestMoves.Count > 0 && AutoPerform && ((ActiveGame.BlackPlaying ? 1 : 0) == PlayAs || PlayAs == 2))
            {
                try
                {
                    Move mv;
                    do
                    {
                        mv = Move.FromUCI(flatBestMoves[moveSelected--]);
                    } while (!ActiveGame.PerformMove(mv) && moveSelected >= 0);
                }
                catch (Exception)
                {
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
                if (ImGui.Button("Reset game"))
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
                ImGui.Checkbox("[Beta] [Internal] Use Bull engine", ref Game.UseBullEngine);
                ImGui.Separator();
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
                        if (ImGui.Button("Perform"))
                        {
                            try
                            {
                                Move mv;
                                do
                                {
                                    mv = Move.FromUCI(flatBestMoves[moveSelected--]);
                                } while (!ActiveGame.PerformMove(mv) && moveSelected >= 0);
                            }
                            catch (Exception)
                            {

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
                    if (ShowOtherMoves && ImGui.BeginChild("movepvc", new Vector2(720, 300)))
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
                            var count = 0;
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
                                            var mv = Move.FromUCI(uci);
                                            ActiveGame.PerformMove(mv);
                                        }
                                        catch (Exception)
                                        {

                                        }
                                    }

                                    count++;
                                }
                            }
                            ImGui.EndTable();
                        }
                        ImGui.EndChild();
                    }

                    ImGui.SliderInt("Depth", ref Depth, 1, 100);
                    ImGui.SliderInt("PV Count", ref PVCount, 1, 15);
                    ImGui.SliderInt("Estimated ELO", ref ELO, 1, 4000);
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
                    UCIEngine.Elo = ELO;
                    UCIEngine.LimitElo = LimitElo;

                    ImGui.SliderInt("Play n'th move", ref NthBestMove, 1, Depth * PVCount);
                    ImGui.Checkbox("Hide best-move", ref HideBestMove);
                    ImGui.SameLine();
                    ImGui.Checkbox("Auto-perform", ref AutoPerform);
                    ImGui.SameLine();
                    if (ImGui.Button("Force re-evaluate"))
                    {
                        EvaluateMove();
                    }
                    ImGui.ListBox("Auto-perform as", ref PlayAs, new List<string> { "White", "Black", "Both" }.ToArray(), 3);
                    UCIEngine.Depth = Depth;
                    ImGui.InputText("Stockfish path", ref Path, 400);
                    UCIEngine.Path = Path;
                    
                    if (ImGui.Button("Download"))
                    {
                        try
                        {
                            var zip = _client.GetByteArrayAsync("https://stockfishchess.org/files/stockfish_15.1_win_x64_avx2.zip").Result;
                            File.WriteAllBytes("C:\\localChess\\stockfish.zip", zip);
                            ZipFile.ExtractToDirectory("C:\\localChess\\stockfish.zip", "C:\\localChess\\");
                        } catch(Exception) {}
                        
                        Path = "C:\\localChess\\stockfish_15.1_win_x64_avx2\\stockfish-windows-2022-x86-64-avx2.exe";
                    }
                }
                ImGui.Separator();
                ImGui.Text("FEN loader");
                ImGui.InputText("FEN string", ref CurrentFen, 128);
                if (ImGui.Button("Load"))
                {
                    var game = Game.FromFen(CurrentFen);
                    Program.ActiveGame = game;
                    ActiveGame = game;
                }
                ImGui.Separator();
                ImGui.Text("[Debug] Move checker");
                ImGui.SliderInt("Check depth", ref CheckDepth, 0, 20);

                if (ImGui.Button("Check"))
                {
                    Console.WriteLine("Depth: " + CheckDepth + ", Moves: " + CountMoves(CheckDepth, CheckDepth, ActiveGame.Copy()));
                }
                ImGui.Separator();

                ImGui.Text("Move history");
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
            ImGui.PopStyleColor();

            if (ShowEvalBar)
            {
                ImGui.GetBackgroundDrawList().AddRectFilled(new Vector2(720, 0), new Vector2(750, 720),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.05f, 1.0f)));
                float Lerp(float a, float b, float t)
                {
                    return a + (b - a) * t;
                }

                _currentEval = Lerp(_currentEval, float.Parse(_eval), 0.2f);
                ImGui.GetBackgroundDrawList().AddRectFilled(new Vector2(720, 720), new Vector2(750, 720 / 2 - (720 / 2 * _currentEval)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.9f, 0.9f, 1.0f)));
            }

            if (Game.GetMouseBoardPosition().HasValue && ActiveGame.SelectedIndex is not null)
            {
                if (ActiveGame.Board[ActiveGame.SelectedIndex.Value] is null) return;
                if (ActiveGame.Board[ActiveGame.SelectedIndex.Value].Type != PieceType.Pawn) return;
                var mousePos = Game.GetMouseBoardPosition();
                var mousePosIndex = Game.GetIndex(mousePos.Value.x, mousePos.Value.y);
                foreach (var move in ActiveGame.LegalMoves)
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
