using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace localChess.Chess
{
    internal class Move
    {
        public int FromIndex { get; set; }
        public int ToIndex { get; set; }
        public bool Valid { get; private set; }
        public static PieceType DefaultPromoteInto { get; set; } = PieceType.Queen;
        public PieceType PromoteInto { get; set; } = DefaultPromoteInto;
        public bool WasPromoted { get; set; }
        public Move(int fromIndex, int toIndex)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            if(fromIndex != toIndex)
                Valid = true;
        }

        public Move(string an, Game game, bool black)
        {
            Create(an, game, black);
        }

        public static Move Create(string an, Game game, bool black)
        {
            var move = new Move(0, 0);

            var ogAn = an;
            an = an.Replace("+", "");
            an = an.Replace("x", "");
            an = an.Replace("#", "");

            if (an[^2] == '=' && char.IsUpper(an[^1]))
            {
                var pieceTypeLetter = an[^1].ToString();
                var pieceType = ConsumePieceType(ref pieceTypeLetter);
                if(pieceType is not null)
                    move.PromoteInto = pieceType.Value;
                an = an[..^2];
            }

            an = an.Replace("=Q", "");
            an = an.Replace("=", "");

            switch (an)
            {
                case "O-O":
                    return Create(!black ? "Kg1" : "Kg8", game, black);
                case "O-O-O":
                    return Create(!black ? "Kc1" : "Kc8", game, black);
            }

            if (an.Length is not (2 or 3 or 4) || !char.IsLower(an[^2]) || !char.IsDigit(an[^1])) return move;

            var type = ConsumePieceType(ref an);

            (int?, int?)? forced = null;

            if (an.Length == 3)
            {
                forced = ConsumeRowOrCol(ref an);
            }

            var pawns = GetPieces(game, black, type, forced);
            var (x, y) = ConsumeRowAndCol(ref an);
            var piece = GetPieceWithMove(game, pawns, x, y);
            move.ToIndex = Game.GetIndex(x, y);

            if (piece is not null)
            {
                move.FromIndex = piece.Value;
                move.Valid = true;
            }
            else
            {
                Console.WriteLine($"ENGINE: Impossible move. [Unknown Position] ({type.ToString()})" + " -> " + game.PrettyPrintPosition(move.ToIndex) + " | " + ogAn);
                //Thread.Sleep(10000);
            }
            return move;

        }

        private static PieceType? ConsumePieceType(ref string input)
        {
            var first = input.ToUpper()[0];
            if (char.IsLetter(first) && char.IsLower(first))
            {
                return PieceType.Pawn;
            }

            input = input[1..];
            return first switch
            {
                'K' => PieceType.King,
                'Q' => PieceType.Queen,
                'R' => PieceType.Rook,
                'B' => PieceType.Bishop,
                'N' => PieceType.Knight,
                _ => null
            };
        }
        public static Piece ConsumePiece(char piece, Game game)
        {
            var type = PieceType.Pawn;

            switch (piece.ToString().ToUpper())
            {
                case "K":
                    type = PieceType.King;
                    break;
                case "Q":
                    type = PieceType.Queen;
                    break;
                case "R":
                    type = PieceType.Rook;
                    break;
                case "B":
                    type = PieceType.Bishop;
                    break;
                case "N":
                    type = PieceType.Knight;
                    break;
                case "P":
                    type = PieceType.Pawn;
                    break;
            }

            return new Piece(type, !char.IsUpper(piece), game);
        }

        private static (int? x, int? y) ConsumeRowOrCol(ref string input)
        {
            var first = input[0];
            input = input[1..];

            if (char.IsDigit(first))
            {
                return (null, 8 - (first - '0'));
            }

            return (first - 'a', null);
        }

        private static (int x, int y) ConsumeRowAndCol(ref string input)
        {
            if (input.Length < 2)
            {
                throw new ArgumentException("Expected string of 2 or more characters");
            }

            if (!(char.IsLetter(input[0]) && char.IsLower(input[0]) && char.IsNumber(input[1])))
            {
                throw new ArgumentException("Expected format of start of string. Expected [lowercase letter][digit][lowercase letter][digit]");
            }

            var xIndex = (input[0] - 'a');
            var yIndex = 8 - (input[1] - '0');

            if (yIndex == 8)
            {
                yIndex = 0;
            }

            input = input[2..];

            return (xIndex, yIndex);
        }

        private static int? GetPieceWithMove(Game game, List<int> pieces, int row, int col)
        {
            var index = Game.GetIndex(row, col);

            foreach (var piece in pieces)
            {
                var moves = Engine.GetLegalMovesFor(piece, game).moves;
                if (moves.Contains(index))
                {
                    return piece;
                }
            }

            return null;
        }

        private static List<int> GetPieces(Game game, bool black, PieceType? type = null, (int? forcedRow, int? ForcedCol)? forced = null)
        {
            var pieces = new List<int>();
            for (var i = 0; i < 8 * 8; i++)
            {
                var (row, col) = Game.GetPos(i);
                if (forced is not null)
                {
                    if (forced.Value.forcedRow is not null && forced.Value.forcedRow != row)
                    {
                        continue;
                    }

                    if (forced.Value.ForcedCol is not null && forced.Value.ForcedCol != col)
                    {
                        continue;
                    }
                }

                if (game.Board[i]?.Black == black)
                {
                    if (type is null || game.Board[i]?.Type == type)
                    {
                        pieces.Add(i);
                    }
                }
            }

            return pieces;
        }

        public static Move FromUCI(string move)
        {
            if(move.Length > 5)
            {
                throw new ArgumentException("UCI move must be at least 4 characters");
            }

            var (startRow, startCol) = ConsumeRowAndCol(ref move);
            var (endRow, endCol) = ConsumeRowAndCol(ref move);

            Move mv = new(startCol * 8 + startRow, endCol * 8 + endRow);

            if (move.Length == 5)
            {
                string p = move[4] + "";
                var pt = ConsumePieceType(ref p);
                if (pt is not null)
                {
                    mv.WasPromoted = true;
                    mv.PromoteInto = pt.Value;
                }
            }

            return mv;
        }

        public string ToUCI()
        {
            var (fromRow, toRow) = (8 - FromIndex / 8, 8 - ToIndex / 8);
            var (fromCol, toCol) = (FromIndex % 8, ToIndex % 8);
            var first = (char)('a' + fromCol) + "" + (char)('0' + fromRow);
            var second = (char)('a' + toCol) + "" + (char)('0' + toRow);
            var prefix = "";

            if (!WasPromoted) return first + second + prefix;
            prefix = PromoteInto switch
            {
                PieceType.King => "k",
                PieceType.Queen => "q",
                PieceType.Rook => "r",
                PieceType.Knight => "n",
                PieceType.Bishop => "b",
                _ => prefix
            };

            return first + second + prefix;
        }
    }
}
