using Raylib_cs;
using ilf;
using localChess.Renderer;
using ilf.pgn.Exceptions;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Threading.Tasks.Sources;
using System.Diagnostics;

namespace localChess.Chess
{
    internal class Game : IRenderable
    {
        public Piece?[] Board = new Piece[8 * 8];
        public int? SelectedIndex;
        public List<int>? LegalMoves;
        public List<Flags>? FlagsList;
        public Dictionary<int, Action<Game>>? SpecialMoves;
        public EngineBridge.EngineType EngineType = EngineBridge.EngineType.Alpaca;

        public bool BlackPlaying { get; set; }
        public ilf.pgn.Data.Game? PgnGame { get; set; }
        public int MoveIndex { get; set; }
        public int GameIndex { get; set; }
        public List<ilf.pgn.Data.Game> PgnGames = new();
        public int? EnPassantIndex { get; set; }
        public bool DidJustEnPassant { get; set; }
        public event EventHandler<Move> OnMove;
        public bool DidJustPromote { get; set; }
        public long LastElapsedTicks = 0;

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

                newBoard[GetIndex(col, row)] = Move.ConsumePiece(chr);
                ++col;
            }

            Game game = new();
            game.Board = newBoard;

            var playing = parts[1];
            game.BlackPlaying = playing == "b";

            var castling = parts[2];
            if (!castling.Contains("K") && game.Board[GetIndex(7, 7)] is not null)
            {
                game.Board[GetIndex(7, 7)].MoveCount = 1;
            }
            if (!castling.Contains("k") && game.Board[GetIndex(7, 7)] is not null)
            {
                game.Board[GetIndex(7, 0)].MoveCount = 1;
            }
            if (!castling.Contains("Q") && game.Board[GetIndex(7, 7)] is not null)
            {
                game.Board[GetIndex(0, 7)].MoveCount = 1;
            }
            if (!castling.Contains("q") && game.Board[GetIndex(7, 7)] is not null)
            {
                game.Board[GetIndex(0, 0)].MoveCount = 1;
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
                At(0, row) = new Piece(PieceType.Rook, row == 0);
                At(1, row) = new Piece(PieceType.Knight, row == 0);
                At(2, row) = new Piece(PieceType.Bishop, row == 0);
                At(3, row) = new Piece(PieceType.Queen, row == 0);
                At(4, row) = new Piece(PieceType.King, row == 0);
                At(5, row) = new Piece(PieceType.Bishop, row == 0);
                At(6, row) = new Piece(PieceType.Knight, row == 0);
                At(7, row) = new Piece(PieceType.Rook, row == 0);
            }

            foreach (var row in new[] { 1, 6 })
            {
                for (var i = 0; i < 8; i++)
                {
                    At(i, row) = new Piece(PieceType.Pawn, row == 1);
                }
            }
        }

        public static (int x , int y)? GetMouseBoardPosition()
        {
            var (x, y) = (Raylib.GetMousePosition().X, Raylib.GetMousePosition().Y);

            if (x > 720 || y > 720)
            {
                return null;
            }

            return ((int)x / 90, (int)y / 90);
        }

        public bool PerformMove(Move move)
        {
            Stopwatch stopwatch = new Stopwatch();
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

        public string PrettyPrintPosition(int index)
        {
            var (x, y) = GetPos(index);
            var typeName = Board[index] is not null ? Board[index]!.Type.ToString() : "Empty";

            var letter = char.ToString((char)('a' + x));
            var number = 8 - y;

            return $"{letter}{number} ({typeName})";
        }

        public void OnTick()
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT) && GetMouseBoardPosition().HasValue)
            {
                var (x, y) = GetMouseBoardPosition().Value;
                if (At(x, y) is not null && At(x, y)!.Black == BlackPlaying)
                {
                    EngineBridge.GetMoves(this, GetIndex(x, y), EngineType);
                }
                
            }

            if (LegalMoves is not null)
            {
                foreach (var move in LegalMoves)
                {
                    var (x, y) = GetPos(move);
                    Raylib.DrawRectangle(x * 90, y * 90, 90, 90, new Color(255, 255, 0, 50));
                }
            }

            if ((Raylib.IsMouseButtonReleased(MouseButton.MOUSE_BUTTON_LEFT) || Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT)) && GetMouseBoardPosition().HasValue)
            {
                if (SelectedIndex is not null)
                {
                    var (x, y) = GetMouseBoardPosition().Value;

                    if ((LegalMoves is not null && LegalMoves.Contains(GetIndex(x, y))) 
                        && !PerformMove(new Move(SelectedIndex.Value, GetIndex(x, y))))
                    {
                        Console.WriteLine("Illegal move!");
                    }
                    //LegalMoves = null;
                    //SpecialMoves = null;
                }

                //SelectedIndex = null;
            }
        }

        public void Render(int x, int y)
        {
            for (var px = 0; px < 8; px++)
            {
                for (var py = 0; py < 8; py++)
                {
                    if(SelectedIndex == GetIndex(px, py) && Raylib.IsMouseButtonDown(MouseButton.MOUSE_BUTTON_LEFT) && GetMouseBoardPosition().HasValue)
                    {
                        continue;
                    }

                    var piece = At(px, py);
                    if (FlagsList is not null && piece is not null)
                    {
                        if (piece.Type == PieceType.King)
                        {
                            if (FlagsList.Contains(Chess.Flags.WhiteInCheck) && !piece.Black)
                            {
                                Raylib.DrawRectangle(px * 90, py * 90, 90, 90, new Color(255, 128, 128, 255));
                            }

                            if (FlagsList.Contains(Chess.Flags.BlackInCheck) && piece.Black)
                            {
                                Raylib.DrawRectangle(px * 90, py * 90, 90, 90, new Color(255, 128, 128, 255));
                            }
                            if (FlagsList.Contains(item: Chess.Flags.WhiteInCheckmate) && !piece.Black)
                            {
                                Raylib.DrawRectangle(px * 90, py * 90, 90, 90, new Color(255, 0, 0, 255));
                            }
                            if (FlagsList.Contains(Chess.Flags.BlackInCheckmate) && piece.Black)
                            {
                                Raylib.DrawRectangle(px * 90, py * 90, 90, 90, new Color(255, 0, 0, 255));
                            }
                        }
                    }
                    

                    piece?.Render(px, py);
                }
            }

            if (SelectedIndex is not null && Raylib.IsMouseButtonDown(MouseButton.MOUSE_BUTTON_LEFT) && GetMouseBoardPosition().HasValue)
            {
                var piece = Board[(int)SelectedIndex];

                piece?.RenderAbsolute((int)Raylib.GetMousePosition().X - 45,
                    (int)Raylib.GetMousePosition().Y - 45);
            }
        }
    }
}
