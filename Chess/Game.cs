using Raylib_cs;
using localChess.Renderer;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Diagnostics;
using localChess.Assets;
using localChess.Networking;
using System.Threading.Tasks.Sources;
using System.Net.WebSockets;

namespace localChess.Chess
{
    internal class Game : IRenderable
    {
        public Piece?[] Board = new Piece[8 * 8];
        public int? SelectedIndex;
        public List<int>? LegalMoves;
        public List<Flags>? FlagsList;
        public Dictionary<int, Action<Game>>? SpecialMoves;
        public int FullMoves = 1;
        public int HalfMoveClock;
        public EngineBridge.EngineType EngineType = EngineBridge.EngineType.Alpaca;
        public int DisplaySize = 720;
        //                to   from  step
        public Dictionary<int, (int, float)> Animations = new();
        public Dictionary<int, (Piece, float)> FadeOuts = new();
        public bool BlackPlaying { get; set; }
        public int? EnPassantIndex { get; set; }
        public bool DidJustEnPassant { get; set; }
        public event EventHandler<Move>? OnMove;
        public bool DidJustPromote { get; set; }
        public bool? LockedColour { get; set; }
        public long LastElapsedTicks;

        private static readonly Texture2D BoardTexture = Raylib.LoadTexture(AssetLoader.GetPath("Board.png"));

        public static Game FromFen(string fen)
        {
            var parts = fen.Split(" ");
            if (parts.Length < 4)
            {
                throw new ArgumentException("Invalid FEN notation, wanted at least 4 segments, found" + parts.Length);
            }

            var board = parts[0];
            int row = 0, col = 0;
            Piece?[] newBoard = new Piece[8 * 8];
            Game game = new();

            foreach (var chr in board)
            {
                if (chr == '/')
                {
                    ++row;
                    col = 0;
                    continue;
                }

                if (char.IsDigit(chr))
                {
                    col += int.Parse(chr.ToString());
                    continue;
                }

                newBoard[GetIndex(col, row)] = Move.ConsumePiece(chr, game);
                ++col;
            }

            
            game.Board = newBoard;

            var playing = parts[1];
            game.BlackPlaying = playing == "b";

            var castling = parts[2];
            if (!castling.Contains("K") && game.Board[GetIndex(7, 7)] is not null)
            {
                game.Board[GetIndex(7, 7)]!.MoveCount = 1;
            }
            if (!castling.Contains("k") && game.Board[GetIndex(7, 0)] is not null)
            {
                game.Board[GetIndex(7, 0)]!.MoveCount = 1;
            }
            if (!castling.Contains("Q") && game.Board[GetIndex(0, 7)] is not null)
            {
                game.Board[GetIndex(0, 7)]!.MoveCount = 1;
            }
            if (!castling.Contains("q") && game.Board[GetIndex(0, 0)] is not null)
            {
                game.Board[GetIndex(0, 0)]!.MoveCount = 1;
            }

            return game;
        }

        public Game()
        {
            CreateDefaultBoard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Game Copy()
        {
            var game = new Game
            {
                Board = Board.Select(a => a?.Clone()).ToArray(),
                BlackPlaying = BlackPlaying,
                FlagsList = FlagsList,
                SpecialMoves = SpecialMoves,
                EnPassantIndex = EnPassantIndex,
                DidJustEnPassant = DidJustEnPassant,
                EngineType = EngineType
            };

            return game;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int x, int y)
        {
            if (x < 0 || y < 0 || x >= 8 || y >= 8) return -1;
            return 8 * y + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) GetPos(int index)
        {
            return (index % 8, index / 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref Piece? At(int x, int y)
        {
            return ref Board[GetIndex(x, y)];
        }

        private void CreateDefaultBoard()
        {
            for (var i = 0; i < 8 * 8; i++)
            {
                Board[i] = null;
            }

            foreach(var row in new[]{ 0, 7 })
            {
                At(0, row) = new Piece(PieceType.Rook, row == 0, this);
                At(1, row) = new Piece(PieceType.Knight, row == 0, this);
                At(2, row) = new Piece(PieceType.Bishop, row == 0, this);
                At(3, row) = new Piece(PieceType.Queen, row == 0, this);
                At(4, row) = new Piece(PieceType.King, row == 0, this);
                At(5, row) = new Piece(PieceType.Bishop, row == 0, this);
                At(6, row) = new Piece(PieceType.Knight, row == 0, this);
                At(7, row) = new Piece(PieceType.Rook, row == 0, this);
            }

            foreach (var row in new[] { 1, 6 })
            {
                for (var i = 0; i < 8; i++)
                {
                    At(i, row) = new Piece(PieceType.Pawn, row == 1, this);
                }
            }
        }

        public (int x , int y)? GetMouseBoardPosition()
        {
            var (x, y) = (Raylib.GetMousePosition().X, Raylib.GetMousePosition().Y);

            if (x > DisplaySize || y > DisplaySize)
            {
                return null;
            }

            return ((int)x / (DisplaySize / 8), (int)y / (DisplaySize / 8));
        }

        public bool PerformMove(Move move)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            DidJustPromote = false;
            if (move.FromIndex == move.ToIndex || Board[move.FromIndex] is null || Board[move.FromIndex]!.Black != BlackPlaying || !move.Valid)
            {
                LegalMoves = null;
                SpecialMoves = null;
                return false;
            }

            if (FlagsList is not null && (FlagsList.Contains(Flags.WhiteInCheckmate) || FlagsList.Contains(Flags.BlackInCheckmate)))
            {
                return false;
            }

            EngineBridge.PerformMove(this, move, EngineType);

            stopwatch.Stop();
            LastElapsedTicks = stopwatch.ElapsedTicks;
            OnMove?.Invoke(this, move);
            return true;
        }

        public string PrettyPrintPosition(int index, bool withTypeName = true)
        {
            var (x, y) = GetPos(index);
            var typeName = Board[index] is not null ? Board[index]!.Type.ToString() : "Empty";

            var letter = char.ToString((char)('a' + x));
            var number = 8 - y;

            return withTypeName ? $"{letter}{number} ({typeName})" : $"{letter}{number}";
        }

        public void OnTick()
        {
            var mbPos = GetMouseBoardPosition();
            if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT) && mbPos.HasValue)
            {
                var (x, y) = mbPos.Value;
                var p = At(x, y);
                if (p is not null && p.Black == BlackPlaying && (LockedColour is null || LockedColour == p.Black))
                {
                    EngineBridge.GetMoves(this, GetIndex(x, y), EngineType);
                }
                
            }

            if (EngineType == EngineBridge.EngineType.Bull)
            {
                for (var i = 0; i < 8 * 8; i++)
                {
                    var p = Board[i];
                    if (p is not null && p.Type == PieceType.King && p.Black == BlackPlaying)
                    {
                        var oldLegalMoves = LegalMoves;
                        EngineBridge.GetMoves(this, i, EngineType, true);
                        LegalMoves = oldLegalMoves;

                    }
                }
            }

            if ((Raylib.IsMouseButtonReleased(MouseButton.MOUSE_BUTTON_LEFT) ||
                 Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT)) &&
                mbPos.HasValue)
            {
                if (SelectedIndex is null) return;
                var (x, y) = mbPos.Value;

                if (Program.Network.Communication.IsConnected())
                {
                    Program.Network.Communication.SendPacket(new MovePacket {FromIndex = SelectedIndex.Value, ToIndex = GetIndex(x, y)});
                }

                if ((LegalMoves is not null && LegalMoves.Contains(GetIndex(x, y))) 
                    && !PerformMove(new Move(SelectedIndex.Value, GetIndex(x, y))))
                {
                    Console.WriteLine(@"Illegal move!");
                }
                //LegalMoves = null;
                //SpecialMoves = null;

                //SelectedIndex = null;
            }
        }

        public string GetFen()
        {
            var buf = "";
            for (var y = 0; y < 8; y++)
            {
                var numOfEmptyPieces = 0;
                for (var x = 0; x < 8; x++)
                {
                    var piece = Board[GetIndex(x, y)];
                    if (numOfEmptyPieces != 0 && piece is not null)
                    {
                        buf += numOfEmptyPieces;
                        numOfEmptyPieces = 0;
                    }

                    if (piece is null)
                    {
                        ++numOfEmptyPieces;
                    }
                    else
                    {
                        var pieceLetter = piece.Type switch
                        {
                            PieceType.Pawn => 'p',
                            PieceType.Rook => 'r',
                            PieceType.Knight => 'n',
                            PieceType.Bishop => 'b',
                            PieceType.Queen => 'q',
                            PieceType.King => 'k',
                            _ => throw new Exception("Invalid piece type")
                        };

                        if (!piece.Black)
                        {
                            pieceLetter = char.ToUpper(pieceLetter);
                        }

                        buf += pieceLetter;
                    }
                }
                if (numOfEmptyPieces != 0)
                {
                    buf += numOfEmptyPieces;
                }
                buf += "/";
            }

            buf = buf[..^1];
            buf += " " + (BlackPlaying ? "b" : "w") + " ";

            if (Board[63]?.Type == PieceType.Rook && Board[63]?.MoveCount == 0 && Board[60]?.Type == PieceType.King && Board[60]?.MoveCount == 0)
            {
                buf += "K";
            }

            if (Board[56]?.Type == PieceType.Rook && Board[56]?.MoveCount == 0 && Board[60]?.Type == PieceType.King && Board[60]?.MoveCount == 0)
            {
                buf += "Q";
            }

            if (Board[7]?.Type == PieceType.Rook && Board[7]?.MoveCount == 0 && Board[4]?.Type == PieceType.King && Board[4]?.MoveCount == 0)
            {
                buf += "k";
            }

            if (Board[0]?.Type == PieceType.Rook && Board[0]?.MoveCount == 0 && Board[4]?.Type == PieceType.King && Board[4]?.MoveCount == 0)
            {
                buf += "q";
            }

            if (buf.EndsWith(" "))
            {
                buf += "- ";
            }
            else
            {
                buf += " ";
            }

            if (EnPassantIndex is not null)
            {
                buf += PrettyPrintPosition(EnPassantIndex.Value, false) + " ";
            }
            else
            {
                buf += "- ";
            }

            buf += HalfMoveClock + " " + FullMoves;
            return buf;
        }

        public void Render(int x, int y)
        {
            var scale = DisplaySize / 720.0f;
            var pieceSize = (int)(90.0f * scale);

            Raylib.DrawTextureEx(BoardTexture, new Vector2(x, y), 0, scale, Color.WHITE);

            void RenderSinglePiece(Piece? piece, int px1, int py1, float alpha = 1.0f, bool animate = true)
            {
                var mbPos = GetMouseBoardPosition();
                var pIndex = GetIndex(px1, py1);
                var (visualPx, visualPy) = ((float)px1, (float)py1);

                if (Animations.ContainsKey(pIndex) && animate)
                {
                    Animations[pIndex] = (Animations[pIndex].Item1,
                        Animations[pIndex].Item2 + 0.01f + ((1.0f - Animations[pIndex].Item2) * 0.2f));
                    var speed = Animations[pIndex].Item2;
                    var (fromX, fromY) = GetPos(Animations[pIndex].Item1);
                    visualPx = Gui.Lerp(fromX, px1, speed);
                    visualPy = Gui.Lerp(fromY, py1, speed);

                    if (Animations[pIndex].Item2 >= 1.0f)
                    {
                        visualPx = px1;
                        visualPy = py1;
                        Animations.Remove(pIndex);
                    }
                }

                if (SelectedIndex == pIndex && Raylib.IsMouseButtonDown(MouseButton.MOUSE_BUTTON_LEFT) && mbPos.HasValue)
                {
                    return;
                }

                if (FlagsList is not null && piece is not null)
                {
                    if (piece.Type == PieceType.King)
                    {
                        if (FlagsList.Contains(Flags.WhiteInCheck) && !piece.Black)
                        {
                            Raylib.DrawRectangle(px1 * pieceSize + x, py1 * pieceSize + y, pieceSize, pieceSize,
                                new Color(255, 128, 128, 255));
                        }

                        if (FlagsList.Contains(Flags.BlackInCheck) && piece.Black)
                        {
                            Raylib.DrawRectangle(px1 * pieceSize + x, py1 * pieceSize + y, pieceSize, pieceSize,
                                new Color(255, 128, 128, 255));
                        }

                        if (FlagsList.Contains(item: Flags.WhiteInCheckmate) && !piece.Black)
                        {
                            Raylib.DrawRectangle(px1 * pieceSize + x, py1 * pieceSize + y, pieceSize, pieceSize,
                                new Color(255, 0, 0, 255));
                        }

                        if (FlagsList.Contains(Flags.BlackInCheckmate) && piece.Black)
                        {
                            Raylib.DrawRectangle(px1 * pieceSize + x, py1 * pieceSize + y, pieceSize, pieceSize,
                                new Color(255, 0, 0, 255));
                        }
                    }
                }


                piece?.Render((visualPx * pieceSize + x) / pieceSize, (visualPy * pieceSize + y) / pieceSize, alpha);
            }

            foreach (var fadeOut in FadeOuts.Select(e => e).ToList())
            {
                var (foX, foY) = GetPos(fadeOut.Key);
                FadeOuts[fadeOut.Key] = (FadeOuts[fadeOut.Key].Item1, FadeOuts[fadeOut.Key].Item2 - 0.05f);
                if (FadeOuts[fadeOut.Key].Item2 <= 0.0f)
                {
                    FadeOuts.Remove(fadeOut.Key);
                }
                else
                {
                    RenderSinglePiece(FadeOuts[fadeOut.Key].Item1, foX, foY, FadeOuts[fadeOut.Key].Item2, false);
                }
                

                
            }

            for (var px = 0; px < 8; px++)
            {
                for (var py = 0; py < 8; py++)
                {
                    RenderSinglePiece(At(px, py), px, py);
                }
            }

            if (LegalMoves is not null)
            {
                foreach (var move in LegalMoves)
                {
                    var (px, py) = GetPos(move);
                    Raylib.DrawRectangle(px * pieceSize, py * pieceSize, pieceSize, pieceSize, new Color(255, 255, 0, 50));
                }
            }

            if (SelectedIndex is not null && Raylib.IsMouseButtonDown(MouseButton.MOUSE_BUTTON_LEFT) && GetMouseBoardPosition().HasValue)
            {
                var piece = Board[(int)SelectedIndex];

                piece?.RenderAbsolute((int)Raylib.GetMousePosition().X - (pieceSize / 2),
                    (int)Raylib.GetMousePosition().Y - (pieceSize / 2));
            }
        }
    }
}
