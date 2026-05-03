using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SudokuApp.Solvers
{
    /// <summary>
    /// Абстрактний базовий клас. Містить спільну логіку (IsValid, Clone, CountSolutions),
    /// щоб підкласи не дублювали код.
    /// </summary>
    public abstract class SudokuSolverBase : ISudokuSolver
    {
        // ── Абстрактні методи — кожен підклас реалізує по-своєму ─────────────

        public abstract SolveStats Solve(int[,] grid, CancellationToken ct = default);

        public abstract (List<SolveStep>? Steps, SolveStats Stats) GetSteps(
            int[,] grid, CancellationToken ct = default);

        // ── Публічні утиліти (потрібні генератору) ────────────────────────────

        /// <summary>Підраховує кількість рішень до ліміту.</summary>
        public static int CountSolutions(int[,] grid, int limit = 2)
        {
            int count = 0;
            CountHelper(Clone(grid), ref count, limit);
            return count;
        }

        /// <summary>Перевіряє, чи можна поставити val у (row, col).</summary>
        public static bool IsValid(int[,] grid, int row, int col, int val)
        {
            for (int c = 0; c < 9; c++)
                if (c != col && grid[row, c] == val) return false;
            for (int r = 0; r < 9; r++)
                if (r != row && grid[r, col] == val) return false;
            int sr = (row / 3) * 3, sc = (col / 3) * 3;
            for (int r = sr; r < sr + 3; r++)
                for (int c = sc; c < sc + 3; c++)
                    if ((r != row || c != col) && grid[r, c] == val) return false;
            return true;
        }

        // ── Захищені утиліти (для підкласів) ─────────────────────────────────

        protected static bool FindEmpty(int[,] g, out int row, out int col)
        {
            for (row = 0; row < 9; row++)
                for (col = 0; col < 9; col++)
                    if (g[row, col] == 0) return true;
            col = 0;
            return false;
        }

        protected static int[,] Clone(int[,] g)
        {
            var c = new int[9, 9];
            for (int r = 0; r < 9; r++)
                for (int col = 0; col < 9; col++)
                    c[r, col] = g[r, col];
            return c;
        }

        /// <summary>Будує SolveStats зі списку кроків та часу.</summary>
        protected static SolveStats BuildStats(
            List<SolveStep> steps, long elapsedMs, bool timedOut)
        {
            int fwd = 0, bwd = 0;
            foreach (var s in steps)
                if (s.IsBacktrack) bwd++; else fwd++;
            return new SolveStats(fwd, bwd, elapsedMs, timedOut);
        }

        // ── Приватні ─────────────────────────────────────────────────────────

        private static void CountHelper(int[,] g, ref int count, int limit)
        {
            if (count >= limit) return;
            if (!FindEmpty(g, out int row, out int col)) { count++; return; }
            for (int n = 1; n <= 9; n++)
                if (IsValid(g, row, col, n))
                {
                    g[row, col] = n;
                    CountHelper(g, ref count, limit);
                    g[row, col] = 0;
                }
        }
    }
}
