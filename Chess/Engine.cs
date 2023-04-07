using System.Runtime.CompilerServices;

namespace localChess.Chess
{
    public enum CaptureMode
    {
        None,
        NoneHook,
        DifferentColour,
        OnlyDifferentColour // ex. pawn attack
    }

    public enum Flags
    {
        WhiteInCheck,
        BlackInCheck,
        WhiteInCheckmate,
        BlackInCheckmate
    }

    internal class ColourHandler
    {
        public bool Check { get; set; }
        public bool Checkmate { get; set; }
        public List<int>? CheckForcedSpaces { get; set; }
        public bool Black { get; }
        public Game Game { get; set; }
        public int X { get; }
        public int Y { get; }

        public int OriginalX { get; }
        public int OriginalY { get; }

        public List<(int x, int y)> Moves { get; }
        public List<(int x, int y)> HookedMoves { get; } = new();

        public ColourHandler(int x, int y, bool black, Game game, List<int>? checkForcedSpaces = null)
        {
            OriginalX = x;
            OriginalY = y;

            if (!black)
            {
                X = 7 - x;
                Y = 7 - y;
            }
            else
            {
                X = OriginalX;
                Y = OriginalY;
            }

            CheckForcedSpaces = checkForcedSpaces;
            Black = black;
            Game = game;
            Moves = new List<(int x, int y)>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Piece? At(int offsetX = 0, int offsetY = 0, bool dontInvert = false)
        {
            if (!Black && !dontInvert)
            {
                //offset_x = -offset_x;
                offsetY = -offsetY;
            }

            if ((OriginalX + offsetX < 0 || OriginalX + offsetX > 7) ||
                (OriginalY + offsetY < 0 || OriginalY + offsetY > 7))
            {
                return null;
            }

            return Game.Board[Game.GetIndex(OriginalX + offsetX, OriginalY + offsetY)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CurrentIndex()
        {
            return (int)GetOffsetIndex()!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int? GetOffsetIndex(int offsetX = 0, int offsetY = 0, bool dontInvert = false)
        {
            if (!Black && !dontInvert)
            {
                //offset_x = -offset_x;
                offsetY = -offsetY;
            }

            if ((OriginalX + offsetX < 0 || OriginalX + offsetX > 7) ||
                (OriginalY + offsetY < 0 || OriginalY + offsetY > 7))
            {
                return null;
            }

            return Game.GetIndex(OriginalX + offsetX, OriginalY + offsetY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOffsetIndexUnbounded(int offsetX = 0, int offsetY = 0, bool dontInvert = false)
        {
            if (!Black && !dontInvert)
            {
                //offset_x = -offset_x;
                offsetY = -offsetY;
            }

            return Game.GetIndex(OriginalX + offsetX, OriginalY + offsetY);
        }

        public (bool added, List<Flags>? flags) AddMove(int offsetX, int offsetY, CaptureMode mode = CaptureMode.DifferentColour, bool collision = true)
        {
            if (!Black)
            {
                //offset_x = -offset_x;
                offsetY = -offsetY;
            }

            if (offsetX == 0 && offsetY == 0)
            {
                return (true, null);
            }

            if ((OriginalX + offsetX < 0 || OriginalX + offsetX > 7) ||
                (OriginalY + offsetY < 0 || OriginalY + offsetY > 7))
            {
                return (false, null);
            }

            var at = At(offsetX, offsetY, true);

            switch (mode)
            {
                case CaptureMode.None when at is null:
                case CaptureMode.NoneHook when at is null:
                case CaptureMode.DifferentColour when
                    (at is null || at.Black != Black):
                case CaptureMode.OnlyDifferentColour when at is not null &&
                                                          at.Black != Black:
                    if (at?.Type == PieceType.King && at.Black != Black)
                    {
                        Check = true;
                        return (false, new List<Flags> { Black ? Flags.WhiteInCheck : Flags.BlackInCheck });
                    }

                    var index = Game.GetIndex(OriginalX + offsetX, OriginalY + offsetY);
                    var ogIndex = Game.GetIndex(OriginalX, OriginalY);

                    if (CheckForcedSpaces is not null &&
                        CheckForcedSpaces.Contains(index))
                    {
                        Check = true;
                        if (CheckForcedSpaces.Contains(index))
                        {
                            var newBoard = Game.Copy();
                            var tmp = newBoard.Board[ogIndex];
                            newBoard.Board[index] = tmp;
                            newBoard.Board[ogIndex] = null;
                            var checkFlag = Engine.IsInCheck(newBoard);

                            if (!checkFlag.Contains(Black ? Flags.BlackInCheck : Flags.WhiteInCheck))
                            {
                                if (mode == CaptureMode.NoneHook)
                                {
                                    HookedMoves.Add((OriginalX + offsetX, OriginalY + offsetY));
                                }
                                Moves.Add((OriginalX + offsetX, OriginalY + offsetY));
                            }
                        }
                    } else if (CheckForcedSpaces is null)
                    {
                        if (mode == CaptureMode.NoneHook)
                        {
                            HookedMoves.Add((OriginalX + offsetX, OriginalY + offsetY));
                        }
                        Moves.Add((OriginalX + offsetX, OriginalY + offsetY));
                    }


                    if (at is not null && collision)
                    {
                        return (false, null);
                    }

                    return (true, null);
                default:
                    return (false, null);
            }
        }

        public (List<int> moves, List<Flags>? flags, List<int> hooks) GetMoves()
        {
            var flagList = Check
                ? new List<Flags>
                    { Black ? Flags.WhiteInCheck : Flags.BlackInCheck}
                : null;

            if (flagList is not null && Checkmate)
            {
                flagList.Add(Black ? Flags.BlackInCheckmate : Flags.WhiteInCheckmate);
            }

            return (Moves.Select(move => Game.GetIndex(move.x, move.y)).ToList(), flagList, HookedMoves.Select(move => Game.GetIndex(move.x, move.y)).ToList());
        }
    }
    internal class Engine
    {
        public static List<Flags> IsInCheck(Game game)
        {
            var flags = new List<Flags>();
            for (var i = 0; i < 8 * 8; i++)
            {
                var result = GetLegalMovesFor(i, game, false);
                if (result.flags is null) continue;
                flags.AddRange(result.flags);
            }
            return flags.Distinct().ToList();
        }

        public static bool IsInCheckmate(Game game, bool black)
        {
            var board = game.Board;
            for (var i = 0; i < 8 * 8; i++)
            {
                if (board[i]?.Black != black) continue;

                var result = GetLegalMovesFor(i, game, false);
                if (result.flags is null) continue;
                foreach (var move in result.moves)
                {
                    var newGame = game.Copy();
                    var tmp = newGame.Board[i];
                    newGame.Board[move] = tmp;
                    newGame.Board[i] = null;
                    var checkFlag = IsInCheck(newGame);
                    if (!checkFlag.Contains(black ? Flags.BlackInCheck : Flags.WhiteInCheck))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static (List<int> moves, List<Flags>? flags, Dictionary<int, Action<Game>>? special, List<int>? hooked) GetLegalMovesFor(int pieceIndex, Game game, bool recurse = true, PieceType promoteInto = PieceType.Queen, int? enPassantIndex = null)
        {
            if (game.FlagsList is not null && (game.FlagsList.Contains(Flags.WhiteInCheckmate) || game.FlagsList.Contains(Flags.BlackInCheckmate)))
            {
                return (new List<int>(), null, null, null);
            }

            var board = game.Board;

            if (board.Length != 8 * 8)
            {
                throw new ArgumentException("Board size needs to be 8 * 8 (64). Got " + board.Length);
            }

            var p = board[pieceIndex];

            if (p is null)
            {
                return (new List<int>(), null, null, null);
            }

            var (x, y) = Game.GetPos(pieceIndex);
            var h = new ColourHandler(x, y, p.Black, game);
            var whiteCheck = false;
            var whiteCheckmate = false;
            var blackCheck = false;
            var blackCheckmate = false;
            var flags = new List<Flags>();

            if (recurse)
            {
                var checkFlag = IsInCheck(game);
                foreach (var flag in checkFlag)
                {
                    switch (flag)
                    {
                        case Flags.WhiteInCheck:
                            whiteCheck = true;
                            break;
                        case Flags.BlackInCheck:
                            blackCheck = true;
                            break;
                        case Flags.WhiteInCheckmate:
                            break;
                        case Flags.BlackInCheckmate:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                
                whiteCheckmate = IsInCheckmate(game, false);
                blackCheckmate = IsInCheckmate(game, true);

            }

            var inCheckOrCheckmate = (p.Black && blackCheck) || (p.Black && blackCheckmate) || (!p.Black && whiteCheck) ||
                                      (!p.Black && whiteCheckmate);

            var special = new Dictionary<int, Action<Game>>();
            

            switch (p.Type)
            {
                case PieceType.Pawn:
                {
                    void PawnPromote(Game b, int ppx, int ppy)
                    {
                        var tmp = b.Board[h.CurrentIndex()];
                        if (tmp == null) return;
                        tmp.Type = promoteInto;
                        b.Board[h.CurrentIndex()] = null;
                        b.Board[h.GetOffsetIndexUnbounded(ppx, ppy)] = tmp;
                    }

                    if (h.AddMove(0, 1, CaptureMode.NoneHook).added)
                    {
                        if (h.Y == 6)
                        {
                            special.Add(h.GetOffsetIndexUnbounded(0, 1), b =>
                            {
                                b.Board[h.GetOffsetIndexUnbounded(0, 1)]!.Type = PieceType.Queen;
                                b.DidJustPromote = true;
                            });
                        }
                        if (h.Y == 1)
                        {
                            if (h.AddMove(0, 2, CaptureMode.NoneHook).added)
                            {
                                special.Add(h.GetOffsetIndexUnbounded(0, 2), g =>
                                {
                                    g.DidJustEnPassant = true;
                                    g.EnPassantIndex = h.GetOffsetIndexUnbounded(0, 2);
                                });
                            }

                        }
                    }
                    h.AddMove(1, 1, CaptureMode.OnlyDifferentColour);
                    h.AddMove(-1, 1, CaptureMode.OnlyDifferentColour);
                    
                    if (h.Y == 6)
                    {
                        special.Add(h.GetOffsetIndexUnbounded(1, 1), b =>
                        {
                            b.DidJustPromote = true;
                            PawnPromote(b, 1, 1);
                        });
                        special.Add(h.GetOffsetIndexUnbounded(-1, 1), b =>
                        {
                            b.DidJustPromote = true;
                            PawnPromote(b, -1, 1);
                        });
                    }
                    

                    h.Game = game;

                    // En passant
                    if (!inCheckOrCheckmate)
                    {
                        var pawnRight = h.At(1);
                        var pawnLeft = h.At(-1);
                        if (pawnRight is not null &&
                            pawnRight.Type == PieceType.Pawn &&
                            pawnRight.MoveCount == 1 && h.Y == 4)
                        {
                            if (h.AddMove(1, 1, CaptureMode.None).added)
                            {
                                special.Add(h.GetOffsetIndexUnbounded(1, 1), g =>
                                {
                                    var tmp = g.Board[h.CurrentIndex()];
                                    g.Board[h.CurrentIndex()] = null;
                                    g.Board[h.GetOffsetIndexUnbounded(1)] = null;
                                    g.Board[h.GetOffsetIndexUnbounded(1, 1)] = tmp;
                                });
                            }
                        }

                        if (pawnLeft is not null &&
                            pawnLeft.Type == PieceType.Pawn &&
                            pawnLeft.MoveCount == 1 && h.Y == 4)
                        {
                            if (h.AddMove(-1, 1, CaptureMode.None).added)
                            {
                                special.Add(h.GetOffsetIndexUnbounded(-1, 1), (g) =>
                                {
                                    var tmp = g.Board[h.CurrentIndex()];
                                    g.Board[h.CurrentIndex()] = null;
                                    g.Board[h.GetOffsetIndexUnbounded(-1)] = null;
                                    g.Board[h.GetOffsetIndexUnbounded(-1, 1)] = tmp;
                                });
                            }
                        }
                    }

                    break;
                }
                case PieceType.Rook:
                {
                    var xOff = 0;
                    var yOff = 0;

                    while (h.AddMove(xOff++, 0).added) { }
                    xOff = 0;
                    while (h.AddMove(xOff--, 0).added) { }

                    while (h.AddMove(0, yOff++).added) { }
                    yOff = 0;
                    while (h.AddMove(0, yOff--).added) { }

                    break;
                }
                case PieceType.Knight:
                    h.AddMove(1, 2);
                    h.AddMove(2, 1);
                    h.AddMove(1, -2);
                    h.AddMove(2, -1);
                    h.AddMove(-1, 2);
                    h.AddMove(-2, 1);
                    h.AddMove(-1, -2);
                    h.AddMove(-2, -1);
                    break;
                case PieceType.Bishop:
                {
                    var xOff = 0;
                    var yOff = 0;

                    while (h.AddMove(xOff++, yOff++).added) { }
                    xOff = 0;
                    yOff = 0;

                    while (h.AddMove(xOff--, yOff--).added) { }
                    xOff = 0;
                    yOff = 0;

                    while (h.AddMove(xOff--, yOff++).added) { }
                    xOff = 0;
                    yOff = 0;

                    while (h.AddMove(xOff++, yOff--).added) { }

                    break;
                }
                case PieceType.Queen:
                {
                    var xOff = 0;
                    var yOff = 0;

                    while (h.AddMove(xOff++, 0).added) { }
                    xOff = 0;
                    while (h.AddMove(xOff--, 0).added) { }

                    while (h.AddMove(0, yOff++).added) { }
                    yOff = 0;
                    while (h.AddMove(0, yOff--).added) { }
                    xOff = 0;
                    yOff = 0;

                    while (h.AddMove(xOff++, yOff++).added) { }
                    xOff = 0;
                    yOff = 0;

                    while (h.AddMove(xOff--, yOff--).added) { }
                    xOff = 0;
                    yOff = 0;

                    while (h.AddMove(xOff--, yOff++).added) { }
                    xOff = 0;
                    yOff = 0;

                    while (h.AddMove(xOff++, yOff--).added) { }

                    break;
                }
                case PieceType.King:
                {
                    h.AddMove(1, -1);
                    h.AddMove(1, 0);
                    h.AddMove(1, 1);
                    h.AddMove(0, 1);
                    h.AddMove(-1, 1);
                    h.AddMove(-1, 0);
                    h.AddMove(-1, -1);
                    h.AddMove(0, -1);

                    if (p.MoveCount == 0 && !inCheckOrCheckmate && recurse)
                    {
                        var potentialQueensideRook = h.At(3);

                        if (potentialQueensideRook is not null && h.At(1) is null && h.At(2) is null
                            && potentialQueensideRook.MoveCount == 0
                            && potentialQueensideRook.Type == PieceType.Rook
                            && potentialQueensideRook.Black == p.Black)
                        {
                            // Check if pass-through square is being attacked

                            var b = game.Copy();
                            var tmp = b.Board[h.GetOffsetIndexUnbounded()];
                            b.Board[h.GetOffsetIndexUnbounded()] = null;
                            b.Board[h.GetOffsetIndexUnbounded(1)] = tmp;
                            var result = IsInCheck(b);

                            if (result.Contains(p.Black ? Flags.BlackInCheck : Flags.WhiteInCheck))
                            {
                                // Being attacked, cannot castle
                            }
                            else
                            {
                                if (h.AddMove(2, 0, CaptureMode.None).added)
                                {
                                    var rookIndex = h.GetOffsetIndexUnbounded(3);
                                    var rookNewIndex = h.GetOffsetIndexUnbounded(1);
                                    var kingIndex = h.GetOffsetIndexUnbounded();

                                    var kingNewIndex = h.GetOffsetIndexUnbounded(2);
                                    special.Add(kingNewIndex, g =>
                                    {
                                        tmp = g.Board[rookIndex];
                                        g.Board[rookIndex] = null;
                                        g.Board[rookNewIndex] = tmp;

                                        tmp = g.Board[kingIndex];
                                        g.Board[kingIndex] = null;
                                        g.Board[kingNewIndex] = tmp;
                                    });
                                }
                            }
                        }

                        var potentialKingsideRook = h.At(-4);

                        if (potentialKingsideRook is not null && h.At(-1) is null && h.At(-2) is null
                            && potentialKingsideRook.MoveCount == 0
                            && potentialKingsideRook.Type == PieceType.Rook
                            && potentialKingsideRook.Black == p.Black)
                        {
                            // Check if pass-through square is being attacked

                            var b = game.Copy();
                            var tmp = b.Board[h.GetOffsetIndexUnbounded()];
                            b.Board[h.GetOffsetIndexUnbounded()] = null;
                            b.Board[h.GetOffsetIndexUnbounded(-1)] = tmp;
                            var result = IsInCheck(b);

                            if (result.Contains(p.Black ? Flags.BlackInCheck : Flags.WhiteInCheck))
                            {
                                // Being attacked, cannot castle
                            }
                            else
                            {
                                if (h.AddMove(-2, 0, CaptureMode.None).added)
                                {
                                    var rookIndex = h.GetOffsetIndexUnbounded(-4);
                                    var rookNewIndex = h.GetOffsetIndexUnbounded(-1);
                                    var kingIndex = h.GetOffsetIndexUnbounded();

                                    var kingNewIndex = h.GetOffsetIndexUnbounded(-2);
                                    special.Add(kingNewIndex, g =>
                                    {
                                        tmp = g.Board[rookIndex];
                                        g.Board[rookIndex] = null;
                                        g.Board[rookNewIndex] = tmp;

                                        tmp = g.Board[kingIndex];
                                        g.Board[kingIndex] = null;
                                        g.Board[kingNewIndex] = tmp;
                                    });
                                }
                            }
                        }

                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }


            var (moves, moveFlags, hooked) = h.GetMoves();

            if(moveFlags is not null)
                flags.AddRange(moveFlags);

            if (whiteCheckmate) flags.Add(Flags.WhiteInCheckmate);
            if (blackCheckmate) flags.Add(Flags.BlackInCheckmate);
            if (whiteCheck) flags.Add(Flags.WhiteInCheck);
            if (blackCheck) flags.Add(Flags.BlackInCheck);

            if (!recurse) return (moves, flags, special, hooked);
            
            var moveCopy = new List<int>(moves);
            foreach (var move in moveCopy)
            {
                var newBoard = game.Copy();
                var tmp = newBoard.Board[pieceIndex];
                newBoard.Board[move] = tmp;
                newBoard.Board[pieceIndex] = null;
                var checkFlag = IsInCheck(newBoard);
                if (checkFlag.Contains(p.Black ? Flags.BlackInCheck : Flags.WhiteInCheck) || 
                    IsInCheckmate(game, p.Black))
                {
                    moves.Remove(move);
                }
            }

            //_cache.Add(game.GetHashCode(pieceIndex), (moves, flags, special));
            return (moves, flags, special, hooked);
        }
    }
}
