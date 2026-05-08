using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SudokuApp.Solvers
{

    public sealed class CspSolver : SudokuSolverBase
    {

        public override SolveStats Solve(int[,] grid, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            int fwd = 0, bwd = 0;

            var d = BuildDomains(grid);
            if (Propagate(grid, d, ct))
                CspBacktrack(grid, d, ct, ref fwd, ref bwd);

            sw.Stop();
            return new SolveStats(fwd, bwd, sw.ElapsedMilliseconds, ct.IsCancellationRequested);
        }

        public override (List<SolveStep>? Steps, SolveStats Stats) GetSteps(
            int[,] grid, CancellationToken ct = default)
        {
            var steps = new List<SolveStep>();
            var clone = Clone(grid);
            var sw = Stopwatch.StartNew();

            var d = BuildDomains(clone);
            bool ok = PropagateWithSteps(clone, d, steps, ct)
                   && CspBacktrackSteps(clone, d, steps, ct);

            sw.Stop();
            bool timedOut = ct.IsCancellationRequested;
            var stats = BuildStats(steps, sw.ElapsedMilliseconds, timedOut);
            return (!ok || timedOut) ? (null, stats) : (steps, stats);
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
                    for (int c = 0; c < 9; c++)
                    {
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
            return true;
        }

        private static bool PropagateWithSteps(
            int[,] grid, HashSet<int>[,] d, List<SolveStep> steps, CancellationToken ct)
        {
            bool changed = true;
            while (changed)
            {
                if (ct.IsCancellationRequested) return false;
                changed = false;
                for (int r = 0; r < 9; r++)
                    for (int c = 0; c < 9; c++)
                    {
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
            return true;
        }

        private static bool PushValue(int[,] g, HashSet<int>[,] d, int row, int col, int val)
        {
            for (int c = 0; c < 9; c++)
                if (c != col && g[row, c] == 0)
                { d[row, c].Remove(val); if (d[row, c].Count == 0) return false; }
            for (int r = 0; r < 9; r++)
                if (r != row && g[r, col] == 0)
                { d[r, col].Remove(val); if (d[r, col].Count == 0) return false; }
            int sr = (row / 3) * 3, sc = (col / 3) * 3;
            for (int r = sr; r < sr + 3; r++)
                for (int c = sc; c < sc + 3; c++)
                    if ((r != row || c != col) && g[r, c] == 0)
                    { d[r, c].Remove(val); if (d[r, c].Count == 0) return false; }
            return true;
        }


        private bool CspBacktrack(
            int[,] grid, HashSet<int>[,] d, CancellationToken ct, ref int fwd, ref int bwd)
        {
            if (ct.IsCancellationRequested) return false;

            int mr = -1, mc = -1, ms = 10;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (grid[r, c] == 0 && d[r, c].Count < ms)
                        (ms, mr, mc) = (d[r, c].Count, r, c);
            if (mr == -1) return true;

            foreach (int val in new List<int>(d[mr, mc]))
            {
                if (ct.IsCancellationRequested) return false;
                if (!IsValid(grid, mr, mc, val)) continue;

                var sg = Clone(grid); var sd = CloneDomains(d);
                grid[mr, mc] = val; d[mr, mc] = new HashSet<int> { val }; fwd++;

                if (PushValue(grid, d, mr, mc, val)
                    && Propagate(grid, d, ct)
                    && CspBacktrack(grid, d, ct, ref fwd, ref bwd)) return true;

                Restore(grid, sg); RestoreDomains(d, sd); bwd++;
            }
            return false;
        }

        private bool CspBacktrackSteps(
            int[,] grid, HashSet<int>[,] d, List<SolveStep> steps, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return false;

            int mr = -1, mc = -1, ms = 10;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (grid[r, c] == 0 && d[r, c].Count < ms)
                        (ms, mr, mc) = (d[r, c].Count, r, c);
            if (mr == -1) return true;

            foreach (int val in new List<int>(d[mr, mc]))
            {
                if (ct.IsCancellationRequested) return false;
                if (!IsValid(grid, mr, mc, val)) continue;

                var sg = Clone(grid); var sd = CloneDomains(d);
                grid[mr, mc] = val; d[mr, mc] = new HashSet<int> { val };
                steps.Add(new SolveStep(mr, mc, val));

                if (PushValue(grid, d, mr, mc, val)
                    && PropagateWithSteps(grid, d, steps, ct)
                    && CspBacktrackSteps(grid, d, steps, ct)) return true;

                steps.Add(new SolveStep(mr, mc, 0, IsBacktrack: true));
                Restore(grid, sg); RestoreDomains(d, sd);
            }
            return false;
        }

        private static HashSet<int>[,] CloneDomains(HashSet<int>[,] d)
        {
            var c = new HashSet<int>[9, 9];
            for (int r = 0; r < 9; r++)
                for (int col = 0; col < 9; col++)
                    c[r, col] = new HashSet<int>(d[r, col]);
            return c;
        }

        private static void Restore(int[,] grid, int[,] saved)
        {
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    grid[r, c] = saved[r, c];
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
