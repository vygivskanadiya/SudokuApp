using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SudokuApp.Solvers
{
    /// <summary>
    /// Алгоритм простого перебору (backtracking).
    /// </summary>
    public sealed class BacktrackingSolver : SudokuSolverBase
    {
        // ── ISudokuSolver ─────────────────────────────────────────────────────

        public override SolveStats Solve(int[,] grid, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            int fwd = 0, bwd = 0;
            SolveRecursive(grid, ct, ref fwd, ref bwd);
            sw.Stop();
            return new SolveStats(fwd, bwd, sw.ElapsedMilliseconds, ct.IsCancellationRequested);
        }

        public override (List<SolveStep>? Steps, SolveStats Stats) GetSteps(
            int[,] grid, CancellationToken ct = default)
        {
            var steps = new List<SolveStep>();
            var clone = Clone(grid);
            var sw = Stopwatch.StartNew();

            bool ok = SolveWithSteps(clone, steps, ct);

            sw.Stop();
            bool timedOut = ct.IsCancellationRequested;
            var stats = BuildStats(steps, sw.ElapsedMilliseconds, timedOut);
            return (!ok || timedOut) ? (null, stats) : (steps, stats);
        }

        // ── Приватна логіка ───────────────────────────────────────────────────

        private bool SolveRecursive(
            int[,] grid, CancellationToken ct, ref int fwd, ref int bwd)
        {
            if (ct.IsCancellationRequested) return false;
            if (!FindEmpty(grid, out int row, out int col)) return true;

            for (int num = 1; num <= 9; num++)
            {
                if (ct.IsCancellationRequested) return false;
                if (!IsValid(grid, row, col, num)) continue;
                grid[row, col] = num; fwd++;
                if (SolveRecursive(grid, ct, ref fwd, ref bwd)) return true;
                grid[row, col] = 0; bwd++;
            }
            return false;
        }

        private bool SolveWithSteps(
            int[,] grid, List<SolveStep> steps, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return false;
            if (!FindEmpty(grid, out int row, out int col)) return true;

            for (int num = 1; num <= 9; num++)
            {
                if (ct.IsCancellationRequested) return false;
                if (!IsValid(grid, row, col, num)) continue;
                grid[row, col] = num;
                steps.Add(new SolveStep(row, col, num));
                if (SolveWithSteps(grid, steps, ct)) return true;
                steps.Add(new SolveStep(row, col, 0, IsBacktrack: true));
                grid[row, col] = 0;
            }
            return false;
        }
    }
}
