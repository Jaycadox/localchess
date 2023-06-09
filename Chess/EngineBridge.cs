﻿using System.Runtime.CompilerServices;

namespace localChess.Chess
{
    internal class EngineBridge
    {
        public enum EngineType
        {
            Alpaca,
            Bull
        }

        public static string GetDescription(EngineType engineType)
        {
            switch (engineType)
            {
                case EngineType.Alpaca:
                {
                    return "The Alpaca internal engine is reliable, but computationally expensive. It is the original internal engine and should be used if performance drops aren't noticed.";
                }
                case EngineType.Bull:
                {
                    
                    return "Bull is an optimized and re-written internal engine with speed being the main priority. It is quite reliable but not as reliable as Alpaca. Should only be used if you notice performance drops.";
                }
            }

            return "No description given";
        }

        public static void GetMoves(Game game, int index, EngineType engineType, bool setFlags = false)
        {
            switch (engineType)
            {
                case EngineType.Alpaca:
                {
                    var result = Engine.GetLegalMovesFor(index, game, true, PieceType.Queen, game.EnPassantIndex);
                    if (result.moves.Count == 0) return;
                    game.FlagsList = result.flags;
                    game.LegalMoves = result.moves;
                    game.SpecialMoves = result.special;
                    game.SelectedIndex = index;
                    break;
                }
                case EngineType.Bull:
                {
                    var result = BullEngine.GetLegalMovesFor((ushort)index, game.Board, game.BlackPlaying, game.EnPassantIndex);
                    if (result.moves.Count != 0)
                    {
                        game.LegalMoves = result.moves.Select((e) => e.Item1.ToIndex).ToList();
                        if(!setFlags)
                            game.SelectedIndex = index;
                    }

                    if (result.flags is null || !setFlags) return;
                    if ((result.flags & BullEngineFlags.Black) != 0)
                    {
                        game.FlagsList = new() { Flags.BlackInCheck };
                        if ((result.flags & BullEngineFlags.Checkmate) != 0)
                        {
                            game.FlagsList.Add(Flags.BlackInCheckmate);
                        }
                    }
                    else
                    {
                        game.FlagsList = new() { Flags.WhiteInCheck };
                        if ((result.flags & BullEngineFlags.Checkmate) != 0)
                        {
                            game.FlagsList.Add(Flags.WhiteInCheckmate);
                        }
                    }

                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void FastBullPerformMove(Game game, Move move, List<(Move, List<Move> moves)> moves, bool animations)
        {
            game.Board = game.Board.Select(a => a).ToArray();
            foreach (var mv in moves)
            {
                var (pMove, moveList) = mv;
                if (move.FromIndex != pMove.FromIndex || move.ToIndex != pMove.ToIndex) continue;
                if (game.Board[move.FromIndex]!.Type == PieceType.Pawn && Math.Abs(move.FromIndex - move.ToIndex) > 10)
                {
                    game.EnPassantIndex = move.ToIndex;
                }
                else
                {
                    game.EnPassantIndex = null;
                }

                ++game.HalfMoveClock;
                if (game.Board[move.FromIndex]!.Type == PieceType.Pawn)
                {
                    game.HalfMoveClock = 0;
                    if (move.ToIndex is < 8 or >= 56)
                    {
                        game.Board[move.FromIndex]!.Type = move.PromoteInto;
                        move.WasPromoted = true;
                    }
                }

                foreach (var mv1 in moveList)
                {
                    game.Board[mv1.FromIndex]!.MoveCount++;
                    if (animations && game.Board[mv1.ToIndex] is not null)
                    {
                        if (game.FadeOuts.ContainsKey(mv1.ToIndex))
                        {
                            game.FadeOuts.Remove(mv1.ToIndex);
                        }
                        game.FadeOuts.Add(mv1.ToIndex, (game.Board[mv1.ToIndex]!.Clone(), 1.0f));
                    }

                    game.Board[mv1.ToIndex] = game.Board[mv1.FromIndex];
                    game.Board[mv1.FromIndex] = null;
                    if (animations)
                    {
                        if(game.Animations.ContainsKey(mv1.ToIndex))
                            game.Animations.Remove(mv1.ToIndex);
                        game.Animations.Add(mv1.ToIndex, (mv1.FromIndex, 0.0f));

                    }
                }

                game.LegalMoves = null;
                game.BlackPlaying = !game.BlackPlaying;
                game.FlagsList = null;
                if (!game.BlackPlaying)
                {
                    ++game.FullMoves;
                }
                break;
            }

            
        }
        public static void PerformMove(Game game, Move move, EngineType engineType)
        {
            switch (engineType)
            {
                case EngineType.Alpaca:
                {
                    var result = Engine.GetLegalMovesFor(move.FromIndex, game, true, move.PromoteInto, game.EnPassantIndex);
                    game.SpecialMoves = result.special;
                    game.LegalMoves = result.moves;

                    if (game.LegalMoves is not null && !game.LegalMoves.Contains(move.ToIndex))
                    {
                        game.LegalMoves = null;
                        game.SpecialMoves = null;
                        Console.WriteLine(@"ENGINE: Illegal move. " + game.PrettyPrintPosition(move.FromIndex) + @" -> " + game.PrettyPrintPosition(move.ToIndex));
                        return;
                    }

                    ++game.HalfMoveClock;
                    if (game.Board[move.FromIndex] is not null)
                    {
                        game.Board[move.FromIndex]!.MoveCount++;
                        if (game.Board[move.FromIndex]!.Type == PieceType.Pawn)
                        {
                            game.HalfMoveClock = 0;
                        }
                    }
                        

                    if (game.SpecialMoves is not null && game.SpecialMoves.ContainsKey(move.ToIndex) &&
                        (result.hooked is null || !result.hooked.Contains(move.ToIndex)))
                    {
                        game.SpecialMoves[move.ToIndex](game);
                    }
                    else
                    {
                        game.Board[move.ToIndex] = game.Board[move.FromIndex];
                        game.Board[move.FromIndex] = null;
                        if (result.hooked is not null && result.hooked.Contains(move.ToIndex) && game.SpecialMoves!.ContainsKey(move.ToIndex))
                        {
                            game.SpecialMoves[move.ToIndex](game);
                        }
                    }
                    move.WasPromoted = game.DidJustPromote;



                    if (!game.DidJustEnPassant)
                    {
                        game.EnPassantIndex = null;
                    }
                    else
                    {
                        game.DidJustEnPassant = false;
                    }


                    result = Engine.GetLegalMovesFor(move.ToIndex, game, true, move.PromoteInto, game.EnPassantIndex);
                    game.LegalMoves = null;
                    game.SpecialMoves = null;
                    game.FlagsList = result.flags;
                    game.BlackPlaying = !game.BlackPlaying;
                    if (!game.BlackPlaying)
                    {
                        ++game.FullMoves;
                    }
                    break;
                }
                case EngineType.Bull:
                {
                    var res = BullEngine.GetLegalMovesFor((ushort)move.FromIndex, game.Board, game.BlackPlaying, game.EnPassantIndex);
                    FastBullPerformMove(game, move, res.moves, true);
                    break;
                }
            }
        }
    }
}
