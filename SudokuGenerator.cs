using System;
using System.Collections.Generic;
using SudokuApp.Analyzers;
using SudokuApp.Solvers;

namespace SudokuApp.Generators
{
    public enum Difficulty { Easy, Medium, Hard }

    public static class SudokuGenerator
    {
        private static readonly Random Rng = new();

        private static readonly Dictionary<Difficulty, int> RemoveTarget = new()
        {
            { Difficulty.Easy,   36 },
            { Difficulty.Medium, 46 },
            { Difficulty.Hard,   52 },
        };

        public static (int[,] Puzzle, int[,] Solution, ActualDifficulty ActualDiff) Generate(
            Difficulty diff)
        {
            var solution = new int[9, 9];
            FillGrid(solution);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                var puzzle = Clone(solution);
                RemoveCells(puzzle, RemoveTarget[diff]);

                var actualDiff = DifficultyAnalyzer.Analyze(puzzle);

                if (DiffMatches(diff, actualDiff))
                    return (puzzle, solution, actualDiff);

                if (actualDiff == ActualDifficulty.Easy && diff != Difficulty.Easy)
                    RemoveCells(puzzle, 4);
            }

            var fallback = Clone(solution);
            RemoveCells(fallback, RemoveTarget[diff]);
            return (fallback, solution, DifficultyAnalyzer.Analyze(fallback));
        }

        // ── Grid filling ─────────────────────────────────────────────────────

        private static bool FillGrid(int[,] g)
        {
            if (!FindEmpty(g, out int row, out int col)) return true;
            foreach (int n in Shuffled())
            {
                // Використовуємо IsValid з базового класу солвера
                if (SudokuSolverBase.IsValid(g, row, col, n))
                {
                    g[row, col] = n;
                    if (FillGrid(g)) return true;
                    g[row, col] = 0;
                }
            }
            return false;
        }

        // ── Cell removal ─────────────────────────────────────────────────────

        private static void RemoveCells(int[,] g, int count)
        {
            var positions = new List<(int R, int C)>();
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (g[r, c] != 0) positions.Add((r, c));
            Shuffle(positions);

            int removed = 0;
            foreach (var (r, c) in positions)
            {
                if (removed >= count) break;
                int backup = g[r, c];
                g[r, c] = 0;

                // Використовуємо CountSolutions з базового класу солвера
                if (SudokuSolverBase.CountSolutions(g, 2) != 1)
                    g[r, c] = backup;
                else
                    removed++;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool DiffMatches(Difficulty wanted, ActualDifficulty actual) =>
            wanted == Difficulty.Easy && actual == ActualDifficulty.Easy ||
            wanted == Difficulty.Medium && actual == ActualDifficulty.Medium ||
            wanted == Difficulty.Hard && actual == ActualDifficulty.Hard;

        private static bool FindEmpty(int[,] g, out int row, out int col)
        {
            for (row = 0; row < 9; row++)
                for (col = 0; col < 9; col++)
                    if (g[row, col] == 0) return true;
            col = 0;
            return false;
        }

        private static int[,] Clone(int[,] g)
        {
            var c = new int[9, 9];
            for (int r = 0; r < 9; r++)
                for (int col = 0; col < 9; col++)
                    c[r, col] = g[r, col];
            return c;
        }

        private static List<int> Shuffled()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            Shuffle(list);
            return list;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
