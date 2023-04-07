using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace localChess.Chess
{
    internal class UCIEngine
    {
        public static int Depth { get; set; } = 10;
        public static Process? StockfishProcess = null;
        public static string Path = "";
        public static bool LimitElo = false;
        public static int Elo = 4000;
        public static int SkillLevel = 20;
        public static bool UseSkillLevel = false;


        public static string? GetEngineResultIfContains(List<string> prompt, string contains, out List<string> lines)
        {
            if (StockfishProcess is { HasExited: false })
            {
                StockfishProcess.Kill();
                StockfishProcess = null;
            }
            lines = new();

            if (!File.Exists(Path))
            {
                
                return "";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = Path,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            StockfishProcess = new Process();
            StockfishProcess.StartInfo = startInfo;
            StockfishProcess.Start();
            foreach (var line in prompt)
                StockfishProcess.StandardInput.WriteLine(line);


            var output = StockfishProcess.StandardOutput.ReadLine();
            while (output != null && !output.StartsWith(contains))
            {
                output = StockfishProcess.StandardOutput.ReadLine();
                if(output is not null)
                    lines.Add(output);
            }

            Console.WriteLine("out: " + output);
            return output;
        }

        public static List<List<string>> GetBestMove(Game game, int pvCount = 3)
        {
            try
            {
                List<string> lines;
                var inputLines = new List<string>
                {
                    "setoption name MultiPV value " + pvCount, "position fen " + game.GetFen(),
                    "go depth " + Depth
                };

                if (LimitElo)
                {
                    inputLines.Insert(0, "setoption name UCI_LimitStrength value true");
                    inputLines.Insert(0, "setoption name UCI_Elo value " + Elo);
                }

                if (UseSkillLevel)
                {
                    inputLines.Insert(0, "setoption name Skill Level value " + SkillLevel);

                }

                string? output = GetEngineResultIfContains(inputLines, "bestmove", out lines);
                lines.Reverse();
                var outMoves = new List<List<string>>();

                int count = 0;
                var moveBuf = new List<List<string>>();
                foreach (var line in lines)
                {
                    if (!line.Contains("multipv"))
                    {
                        continue;
                    }

                    var moveList = line.Split(" pv ")[1].Split(" ");
                    moveBuf.Add(moveList.ToList());
                    count++;

                    if (count == pvCount)
                    {
                        moveBuf.Reverse();
                        outMoves.AddRange(moveBuf);
                        moveBuf.Clear();
                        count = 0;
                    }
                }

                if (outMoves.Count == 0)
                {
                    outMoves.Add(new() { output.Split(" ")[1] });
                }
                
                return outMoves;
            }
            catch (Exception)
            {
                return new();
            }
            
        }

        public static float Eval(Game game)
        {
            try
            {
                List<string> lines;
                var output = GetEngineResultIfContains(new List<string>
                {
                    "uci", "position fen " + game.GetFen(), "eval"
                }, "Final evaluation", out lines);

                var outputParts = output.Split('(');
                var evals = outputParts[0].Split("evaluation")[1].Trim().Trim('+').Trim('-');

                var score = float.Parse(evals);
                if (output.Contains("black"))
                {
                    score = -score;
                }
                if (output.Contains("-"))
                {
                    score = -score;
                }
                return score;
            }
            catch (Exception)
            {
                return -999;
            }
            

            
        }
    }
}
