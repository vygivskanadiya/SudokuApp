using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SudokuApp.Solvers
{
    public enum SolverAlgorithm { Backtracking, ConstraintPropagation }

    public record SolveStep(int Row, int Col, int Value, bool IsBacktrack = false);

    public record SolveStats(
        int ForwardSteps,
        int BacktrackSteps,
        long ElapsedMs,
        bool TimedOut
    );

    public static class SudokuSolver
    {
        private const int TimeoutSeconds = 120;

        // ═══════════════════════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════════════════════

        public static SolveStats Solve(int[,] grid, SolverAlgorithm alg)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var sw = Stopwatch.StartNew();
            int fwd = 0, bwd = 0;

            bool ok = alg == SolverAlgorithm.Backtracking
                ? SolveBacktracking(grid, cts.Token, ref fwd, ref bwd)
                : SolveCSP(grid, cts.Token, ref fwd, ref bwd);

            sw.Stop();
            // Якщо ok == false і токен скасовано — це таймаут
            return new SolveStats(fwd, bwd, sw.ElapsedMilliseconds, cts.IsCancellationRequested);
        }

        public static (List<SolveStep>? Steps, SolveStats Stats) GetSteps(
            int[,] grid, SolverAlgorithm alg, CancellationToken externalCt = default)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalCt, timeoutCts.Token);
            var ct = linked.Token;

            var steps = new List<SolveStep>();
            var clone = Clone(grid);
            var sw = Stopwatch.StartNew();

            bool ok = alg == SolverAlgorithm.Backtracking
                ? BtSteps(clone, steps, ct)
                : CspSteps(clone, steps, ct);

            sw.Stop();
            bool timedOut = ct.IsCancellationRequested;

            int fwd = 0, bwd = 0;
            foreach (var s in steps)
                if (s.IsBacktrack) bwd++; else fwd++;

            var stats = new SolveStats(fwd, bwd, sw.ElapsedMilliseconds, timedOut);
            return (!ok || timedOut) ? (null, stats) : (steps, stats);
        }

        public static int CountSolutions(int[,] grid, int limit = 2)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            int count = 0;
            CountHelper(Clone(grid), ref count, limit, cts.Token);

            if (cts.IsCancellationRequested && count < limit)
                return limit;

            return count;
        }

        private static void CountHelper(int[,] grid, ref int count, int limit, CancellationToken ct)
        {
            if (ct.IsCancellationRequested || count >= limit) return;
            if (!FindEmpty(grid, out int row, out int col)) { count++; return; }

            for (int num = 1; num <= 9; num++)
            {
                if (ct.IsCancellationRequested) return;

                if (IsValid(grid, row, col, num))
                {
                    grid[row, col] = num;
                    CountHelper(grid, ref count, limit, ct);
                    grid[row, col] = 0;
                }
            }
        }

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

        // ═══════════════════════════════════════════════════════════
        //  Backtracking
        // ═══════════════════════════════════════════════════════════

        private static bool SolveBacktracking(int[,] grid, CancellationToken ct, ref int fwd, ref int bwd)
        {
            if (ct.IsCancellationRequested) return false;
            if (!FindEmpty(grid, out int row, out int col)) return true;

            for (int num = 1; num <= 9; num++)
            {
                if (ct.IsCancellationRequested) return false;
                if (!IsValid(grid, row, col, num)) continue;

                grid[row, col] = num; fwd++;
                if (SolveBacktracking(grid, ct, ref fwd, ref bwd)) return true;

                // Критично: перевірка після повернення з рекурсії
                if (ct.IsCancellationRequested) return false;

                grid[row, col] = 0; bwd++;
            }
            return false;
        }

        private static bool BtSteps(int[,] grid, List<SolveStep> steps, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return false;
            if (!FindEmpty(grid, out int row, out int col)) return true;

            for (int num = 1; num <= 9; num++)
            {
                if (ct.IsCancellationRequested) return false;
                if (!IsValid(grid, row, col, num)) continue;

                grid[row, col] = num;
                steps.Add(new SolveStep(row, col, num));

                if (BtSteps(grid, steps, ct)) return true;

                if (ct.IsCancellationRequested) return false;

                steps.Add(new SolveStep(row, col, 0, IsBacktrack: true));
                grid[row, col] = 0;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════
        //  CSP
        // ═══════════════════════════════════════════════════════════

        private static bool SolveCSP(int[,] grid, CancellationToken ct, ref int fwd, ref int bwd)
        {
            var d = BuildDomains(grid);
            if (!Propagate(grid, d, ct)) return false;
            return CspBacktrack(grid, d, ct, ref fwd, ref bwd);
        }

        private static bool CspSteps(int[,] grid, List<SolveStep> steps, CancellationToken ct)
        {
            var d = BuildDomains(grid);
            if (!PropagateWithSteps(grid, d, steps, ct)) return false;
            return CspBacktrackSteps(grid, d, steps, ct);
        }

        private static HashSet<int>[,] BuildDomains(int[,] grid)
        {
            var d = new HashSet<int>[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                {
                    if (grid[r, c] != 0) { d[r, c] = new HashSet<int> { grid[r, c] }; continue; }
                    d[r, c] = new HashSet<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                    EliminateKnown(grid, r, c, d[r, c]);
                }
            return d;
        }

        private static void EliminateKnown(int[,] grid, int row, int col, HashSet<int> domain)
        {
            for (int c = 0; c < 9; c++) if (grid[row, c] != 0) domain.Remove(grid[row, c]);
            for (int r = 0; r < 9; r++) if (grid[r, col] != 0) domain.Remove(grid[r, col]);
            int sr = (row / 3) * 3, sc = (col / 3) * 3;
            for (int r = sr; r < sr + 3; r++)
                for (int c = sc; c < sc + 3; c++)
                    if (grid[r, c] != 0) domain.Remove(grid[r, c]);
        }

        private static bool Propagate(int[,] grid, HashSet<int>[,] d, CancellationToken ct)
        {
            bool changed = true;
            while (changed)
            {
                if (ct.IsCancellationRequested) return false;
                changed = false;
                for (int r = 0; r < 9; r++)
                {
                    for (int c = 0; c < 9; c++)
                    {
                        if (ct.IsCancellationRequested) return false;
                        if (grid[r, c] != 0) continue;
                        if (d[r, c].Count == 0) return false;
                        if (d[r, c].Count == 1)
                        {
                            int val = First(d[r, c]);
                            grid[r, c] = val;
                            if (!PushValue(grid, d, r, c, val)) return false;
                            changed = true;
                        }
                    }
                }
            }
            return true;
        }

        private static bool PropagateWithSteps(int[,] grid, HashSet<int>[,] d, List<SolveStep> steps, CancellationToken ct)
        {
            bool changed = true;
            while (changed)
            {
                if (ct.IsCancellationRequested) return false;
                changed = false;
                for (int r = 0; r < 9; r++)
                {
                    for (int c = 0; c < 9; c++)
                    {
                        if (ct.IsCancellationRequested) return false;
                        if (grid[r, c] != 0) continue;
                        if (d[r, c].Count == 0) return false;
                        if (d[r, c].Count == 1)
                        {
                            int val = First(d[r, c]);
                            grid[r, c] = val;
                            steps.Add(new SolveStep(r, c, val));
                            if (!PushValue(grid, d, r, c, val)) return false;
                            changed = true;
                        }
                    }
                }
            }
            return true;
        }

        private static bool PushValue(int[,] g, HashSet<int>[,] d, int row, int col, int val)
        {
            for (int c = 0; c < 9; c++)
                if (c != col && g[row, c] == 0) { d[row, c].Remove(val); if (d[row, c].Count == 0) return false; }
            for (int r = 0; r < 9; r++)
                if (r != row && g[r, col] == 0) { d[r, col].Remove(val); if (d[r, col].Count == 0) return false; }
            int sr = (row / 3) * 3, sc = (col / 3) * 3;
            for (int r = sr; r < sr + 3; r++)
                for (int c = sc; c < sc + 3; c++)
                    if ((r != row || c != col) && g[r, c] == 0)
                    { d[r, c].Remove(val); if (d[r, c].Count == 0) return false; }
            return true;
        }

        private static bool CspBacktrack(int[,] grid, HashSet<int>[,] d, CancellationToken ct, ref int fwd, ref int bwd)
        {
            if (ct.IsCancellationRequested) return false;

            int mr = -1, mc = -1, ms = 10;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (grid[r, c] == 0 && d[r, c].Count < ms)
                        (ms, mr, mc) = (d[r, c].Count, r, c);

            if (mr == -1) return true;

            var sg = Clone(grid);
            var sd = CloneDomains(d);

            foreach (int val in new List<int>(d[mr, mc]))
            {
                if (ct.IsCancellationRequested) return false;
                if (!IsValid(grid, mr, mc, val)) continue;

                grid[mr, mc] = val;
                d[mr, mc] = new HashSet<int> { val };
                fwd++;

                if (PushValue(grid, d, mr, mc, val) && Propagate(grid, d, ct)
                    && CspBacktrack(grid, d, ct, ref fwd, ref bwd)) return true;

                // Перевірка після рекурсії: негайний вихід без відновлення, якщо скасовано
                if (ct.IsCancellationRequested) return false;

                Restore(grid, sg);
                RestoreDomains(d, sd);
                bwd++;
            }
            return false;
        }

        private static bool CspBacktrackSteps(int[,] grid, HashSet<int>[,] d, List<SolveStep> steps, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return false;

            int mr = -1, mc = -1, ms = 10;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (grid[r, c] == 0 && d[r, c].Count < ms)
                        (ms, mr, mc) = (d[r, c].Count, r, c);

            if (mr == -1) return true;

            var sg = Clone(grid);
            var sd = CloneDomains(d);

            foreach (int val in new List<int>(d[mr, mc]))
            {
                if (ct.IsCancellationRequested) return false;
                if (!IsValid(grid, mr, mc, val)) continue;

                grid[mr, mc] = val;
                d[mr, mc] = new HashSet<int> { val };
                steps.Add(new SolveStep(mr, mc, val));

                if (PushValue(grid, d, mr, mc, val) && PropagateWithSteps(grid, d, steps, ct)
                    && CspBacktrackSteps(grid, d, steps, ct)) return true;

                if (ct.IsCancellationRequested) return false;

                steps.Add(new SolveStep(mr, mc, 0, IsBacktrack: true));
                Restore(grid, sg);
                RestoreDomains(d, sd);
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════

        private static bool FindEmpty(int[,] grid, out int row, out int col)
        {
            for (row = 0; row < 9; row++)
                for (col = 0; col < 9; col++)
                    if (grid[row, col] == 0) return true;
            col = -1; row = -1; return false;
        }

        private static int[,] Clone(int[,] g)
        {
            var c = new int[9, 9];
            Buffer.BlockCopy(g, 0, c, 0, 9 * 9 * sizeof(int));
            return c;
        }

        private static void Restore(int[,] grid, int[,] saved)
        { Buffer.BlockCopy(saved, 0, grid, 0, 9 * 9 * sizeof(int)); }

        private static HashSet<int>[,] CloneDomains(HashSet<int>[,] d)
        {
            var c = new HashSet<int>[9, 9];
            for (int r = 0; r < 9; r++)
                for (int col = 0; col < 9; col++)
                    c[r, col] = new HashSet<int>(d[r, col]);
            return c;
        }

        private static void RestoreDomains(HashSet<int>[,] d, HashSet<int>[,] saved)
        {
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    d[r, c] = new HashSet<int>(saved[r, c]);
        }

        private static T First<T>(HashSet<T> set)
        {
            foreach (var x in set) return x;
            throw new InvalidOperationException("Empty set");
        }
    }
}
