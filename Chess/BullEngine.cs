using System.Runtime.CompilerServices;

namespace localChess.Chess
{
    internal class BullEngineFlags
    {
        public const ushort White = 0;
        public const ushort Black = 1;
        public const ushort Checkmate = 2;
    }
    internal class BullEngine
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public static (List<(Move, List<Move>)> moves, ushort? flags) GetLegalMovesFor(ushort index, Piece?[] board,
            bool black, int? enPassantIndex = null, bool onlyCareAboutCheck = false)
        {
            Dictionary<ushort, Dictionary<ushort, List<Move>>> pieceMoves = new();
            var inCheck = false;
            var kingIndex = 0;
            var (kingX, kingY) = (0, 0);
            if (onlyCareAboutCheck || board[index]?.Type == PieceType.King)
            {
                for (ushort i = 0; i < 8 * 8; i++)
                {
                    if (board[i] is null || board[i]?.Black != black || board[i]?.Type != PieceType.King) continue;
                    kingIndex = i;
                    (kingX, kingY) = Game.GetPos(kingIndex);
                    break;
                }
            }

            for (ushort i = 0; i < 8 * 8; i++)
            {
                if (i != index && board[i]?.Black == black || board[i] is null) continue;
                if (i != index)
                {
                    var (x, y) = Game.GetPos(i);
                    if (board[i]!.Type == PieceType.Pawn && !(x == kingX + 1 || x == kingY - 1))
                    {
                        continue;
                    }
                    if (board[i]!.Type == PieceType.Rook && Math.Abs(kingX - x) > 1 && Math.Abs(kingY - y) > 1)
                    {
                        continue;
                    }
                    if (board[i]!.Type == PieceType.Knight && Math.Abs(kingX - x) > 2 && Math.Abs(kingY - y) > 2)
                    {
                        continue;
                    }
                    if (board[i]!.Type == PieceType.Bishop && Math.Abs(kingX - x) != Math.Abs(kingY - y))
                    {
                        continue;
                    }
                    if (board[i]!.Type == PieceType.Queen && Math.Abs(kingX - x) != Math.Abs(kingY - y) && Math.Abs(kingX - x) > 1 && Math.Abs(kingY - y) > 1)
                    {
                        continue;
                    }
                }

                var result = InternalLegalMovesFor(i, board, onlyCareAboutCheck);
                if (!inCheck && result.flags is not null)
                {
                    if ((result.flags == 1 && black) || (result.flags == 0 && !black))
                    {
                        inCheck = true;
                        if (onlyCareAboutCheck)
                            break;
                    }
                        
                }
                if(!onlyCareAboutCheck)
                    pieceMoves.Add(i, result.moves);
            }

            if (onlyCareAboutCheck)
            {
                return (new List<(Move, List<Move>)>(), inCheck ? black ? BullEngineFlags.Black : BullEngineFlags.White : null);
            }

            List<ushort> enemyIndexMoves = new();
            foreach (var (idx, moves) in pieceMoves)
            {
                if (board[idx]?.Black != black)
                {
                    enemyIndexMoves.AddRange(moves.Keys.ToList());
                }
            }

            var pMoves = pieceMoves[index];
            foreach (var move in pMoves)
            {
                Dictionary<ushort, Piece?> ogPieces = new();
                foreach (var mv in move.Value)
                {
                    ogPieces.Add((ushort)mv.ToIndex, board[mv.ToIndex]?.Clone());
                    board[mv.ToIndex] = board[mv.FromIndex];
                    board[mv.FromIndex] = null;

                    var res = GetLegalMovesFor((ushort)mv.ToIndex, board, black, enPassantIndex, true);
                    board[mv.FromIndex] = board[mv.ToIndex]?.Clone();
                    board[mv.ToIndex] = ogPieces[(ushort)mv.ToIndex];

                    if (res.flags is not null)
                    {
                        pieceMoves[index].Remove(move.Key);
                    }
                }
            }

            if (enPassantIndex is not null && board[index]!.Type == PieceType.Pawn && !inCheck && !onlyCareAboutCheck)
            {
                if (index - 1 == enPassantIndex || index + 1 == enPassantIndex)
                {
                    if (black)
                    {
                        if (index + 1 == enPassantIndex)
                        {
                            pieceMoves[index].Add((ushort)(index + 9), new()
                            {
                                new(index + 1, index + 9),
                                new(index, index + 9),
                            });
                        }
                        else
                        {
                            pieceMoves[index].Add((ushort)(index + 7), new()
                            {
                                new(index - 1, index + 7),
                                new(index, index + 7),
                            });
                        }
                    }
                    else
                    {
                        if (index - 1 == enPassantIndex)
                        {
                            pieceMoves[index].Add((ushort)(index - 9), new()
                            {
                                new(index - 1, index - 9),
                                new(index, index - 9),
                            });
                        }
                        else
                        {
                            pieceMoves[index].Add((ushort)(index - 7), new()
                            {
                                new(index + 1, index - 7),
                                new(index, index - 7),
                            });
                        }
                    }
                }
            }

            if (!onlyCareAboutCheck && !inCheck && index == kingIndex && kingIndex is 4 or 60)
            {
                if (board[kingIndex]!.Type == PieceType.King && (board[kingIndex]!.MoveCount == 0))
                {
                    var kingSideRook = Game.GetIndex(kingX + 3, kingY);
                    var filled = board[Game.GetIndex(kingX + 1, kingY)] is not null || board[Game.GetIndex(kingX + 2, kingY)] is not null;
                    var check = enemyIndexMoves.Contains((ushort)Game.GetIndex(kingX + 1, kingY)) || enemyIndexMoves.Contains((ushort)Game.GetIndex(kingX + 2, kingY));
                    if (!check && !filled && kingSideRook < 8 * 8 && board[kingSideRook] is not null && board[kingSideRook]!.Type == PieceType.Rook && board[kingSideRook]!.MoveCount == 0)
                    {
                        var idx = Game.GetIndex(kingX + 2, kingY);
                        pieceMoves[index].Add((ushort)idx, new()
                        {
                            new Move(kingIndex, idx),
                            new Move(kingSideRook, kingSideRook - 2),
                        });
                    }

                    var queenSideRook = Game.GetIndex(kingX - 4, kingY);
                    filled = board[Game.GetIndex(kingX - 1, kingY)] is not null || board[Game.GetIndex(kingX - 2, kingY)] is not null || board[Game.GetIndex(kingX - 3, kingY)] is not null;
                    check = enemyIndexMoves.Contains((ushort)Game.GetIndex(kingX - 1, kingY)) || enemyIndexMoves.Contains((ushort)Game.GetIndex(kingX - 2, kingY)) || enemyIndexMoves.Contains((ushort)Game.GetIndex(kingX - 3, kingY));
                    if (!check && !filled && queenSideRook >= 0 && board[queenSideRook] is not null && board[queenSideRook]!.Type == PieceType.Rook && board[queenSideRook]!.MoveCount == 0)
                    {
                        var idx = Game.GetIndex(kingX - 2, kingY);
                        pieceMoves[index].Add((ushort)idx, new()
                        {
                            new Move(kingIndex, idx),
                            new Move(queenSideRook, queenSideRook + 3),
                        });
                    }
                }
            }

            if (pieceMoves.Count == 0)
            {
                Console.WriteLine(@"checkmate 1");
            }

            var checkMate = false;

            if (inCheck)
            {
                checkMate = pieceMoves.Where(moves => board[moves.Key]!.Black == black).All(moves => moves.Value.Count == 0);
            }
            ushort? flags = null;
            if (inCheck)
            {
                flags = black ? BullEngineFlags.Black : BullEngineFlags.White;
                if (checkMate)
                {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
                    flags |= BullEngineFlags.Checkmate;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
                }

            }

            return (pieceMoves[index].Select(x => (new Move(index, x.Key), x.Value)).ToList(), flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static (Dictionary<ushort, List<Move>> moves, ushort? flags) InternalLegalMovesFor(ushort index, Piece?[] board, bool onlyCareAboutCheck = false)
        {
            Dictionary<ushort, List<Move>> moves = new();

            uint? flags = null;

            [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
            (bool moved, bool intersecting) AddCapturingMove(int from, int to, bool canCapture = true, bool forcedCapture = false)
            {
                if (onlyCareAboutCheck)
                {
                    if (0 > from || 0 > to) return (false, false);
                    if (8 * 8 <= from || 8 * 8 <= to || board[to] is null || board[to]?.Black == board[from]!.Black) return (false, board[to] is not null);
                    if (board[to]!.Type == PieceType.King)
                    {
                        flags ??= 0;
                        flags |= board[to]!.Black ? BullEngineFlags.Black : BullEngineFlags.White;
                        return (false, true);
                    }
                    else
                    {
                        return (true, true);
                    }
                }
                if (!canCapture && onlyCareAboutCheck)
                {
                    return (false, false);
                }
                if (0 > from || 0 > to) return (false, false);
                if (8 * 8 <= from || 8 * 8 <= to) return (false, false);
                var inter = board[to] is not null;
                if (board[from] is null) return (false, inter);
                if (board[to] is not null && board[to]!.Black == board[from]!.Black) return (false, inter);
                if (board[to] is not null && board[to]!.Type == PieceType.King && board[to]!.Black != board[from]!.Black)
                {
                    flags ??= 0;
                    flags |= board[to]!.Black ? BullEngineFlags.Black : BullEngineFlags.White;
                    return (false, true);
                }

                if (!canCapture && inter) return (false, inter);
                if (forcedCapture && !inter)
                {
                    return (false, inter);
                }

                if (!onlyCareAboutCheck)
                {
                    moves.Add((ushort)to, new() { new(from, to) });
                }
                else
                {
                    moves.Add((ushort)to, new());
                }
                
                return (true, inter);
            }

            var piece = board[index];
            if (piece == null)
            {
                return (new(), (ushort?)flags);
            }

            var black = piece.Black;

            var (x, y) = Game.GetPos(index);
            
            switch (piece.Type)
            {
                case PieceType.Pawn when !black:
                {
                    if (AddCapturingMove(index, Game.GetIndex(x, y - 1), false).moved && y == 6)
                    {
                        AddCapturingMove(index, Game.GetIndex(x, y - 2));
                    }
                    AddCapturingMove(index, Game.GetIndex(x + 1, y - 1), true, true);
                    AddCapturingMove(index, Game.GetIndex(x - 1, y - 1), true, true);
                    break;
                }
                case PieceType.Pawn:
                {
                    if (AddCapturingMove(index, Game.GetIndex(x, y + 1), false).moved && y == 1)
                    {
                        AddCapturingMove(index, Game.GetIndex(x, y + 2), false);
                    }
                    AddCapturingMove(index, Game.GetIndex(x + 1, y + 1), true, true);
                    AddCapturingMove(index, Game.GetIndex(x - 1, y + 1), true, true);
                    break;
                }
                case PieceType.Knight:
                    AddCapturingMove(index, Game.GetIndex(x + 1, y + 2));
                    AddCapturingMove(index, Game.GetIndex(x - 1, y + 2));
                    AddCapturingMove(index, Game.GetIndex(x + 1, y - 2));
                    AddCapturingMove(index, Game.GetIndex(x - 1, y - 2));
                    AddCapturingMove(index, Game.GetIndex(x + 2, y - 1));
                    AddCapturingMove(index, Game.GetIndex(x + 2, y + 1));
                    AddCapturingMove(index, Game.GetIndex(x - 2, y - 1));
                    AddCapturingMove(index, Game.GetIndex(x - 2, y + 1));
                    break;
                case PieceType.Rook or PieceType.Bishop or PieceType.Queen or PieceType.King:
                {
                    ushort hitMask = 0;
                    const ushort dUp = 1;
                    const ushort dDown = 2;
                    const ushort dLeft = 4;
                    const ushort dRight = 8;
                    const ushort dLeftUp = 16;
                    const ushort dLeftDown = 32;
                    const ushort dRightUp = 64;
                    const ushort dRightDown = 128;

                    if (piece.Type is PieceType.Rook or PieceType.Queen or PieceType.King)
                    {
                        hitMask |= dUp | dDown | dLeft | dRight;
                    }

                    if (piece.Type is PieceType.Bishop or PieceType.Queen or PieceType.King)
                    {
                        hitMask |= dLeftUp | dLeftDown | dRightUp | dRightDown;
                    }

                    for (var i = 1; i < (piece.Type == PieceType.King ? 2 : 8); i++)
                    {
                        if ((hitMask & dDown) != 0 && AddCapturingMove(index, Game.GetIndex(x, y + i)).intersecting)
                        {
                            hitMask &= dDown ^ ushort.MaxValue;
                        }

                        if ((hitMask & dUp) != 0 && AddCapturingMove(index, Game.GetIndex(x, y - i)).intersecting)
                        {
                            hitMask &= dUp ^ ushort.MaxValue;
                        }

                        if ((hitMask & dLeft) != 0 && AddCapturingMove(index, Game.GetIndex(x - i, y)).intersecting)
                        {
                            hitMask &= dLeft ^ ushort.MaxValue;
                        }

                        if ((hitMask & dRight) != 0 && AddCapturingMove(index, Game.GetIndex(x + i, y)).intersecting)
                        {
                            hitMask &= dRight ^ ushort.MaxValue;
                        }

                        if ((hitMask & dLeftDown) != 0 && AddCapturingMove(index, Game.GetIndex(x - i, y + i)).intersecting)
                        {
                            hitMask &= dLeftDown ^ ushort.MaxValue;
                        }

                        if ((hitMask & dLeftUp) != 0 && AddCapturingMove(index, Game.GetIndex(x - i, y - i)).intersecting)
                        {
                            hitMask &= dLeftUp ^ ushort.MaxValue;
                        }

                        if ((hitMask & dRightDown) != 0 && AddCapturingMove(index, Game.GetIndex(x + i, y + i)).intersecting)
                        {
                            hitMask &= dRightDown ^ ushort.MaxValue;
                        }

                        if ((hitMask & dRightUp) != 0 && AddCapturingMove(index, Game.GetIndex(x + i, y - i)).intersecting)
                        {
                            hitMask &= dRightUp ^ ushort.MaxValue;
                        }

                    }

                    break;
                }
            }

            return (moves, (ushort?)flags);
        }
    }
}
