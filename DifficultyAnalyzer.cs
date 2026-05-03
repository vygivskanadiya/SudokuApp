using System.Collections.Generic;

namespace SudokuApp.Analyzers
{
    /// <summary>
    /// Визначає реальну складність судоку на основі технік,
    /// необхідних для розв'язання без перебору.
    ///
    /// Easy   – лише «голі одиночки» (naked singles):
    ///          клітинка має рівно 1 можливий кандидат.
    ///
    /// Medium – потрібні «приховані одиночки» (hidden singles):
    ///          цифра може стояти тільки в одній клітинці рядка/стовпця/блоку.
    ///
    /// Hard   – потрібні складніші техніки (naked pairs, pointing pairs тощо)
    ///          або обов'язковий перебір (trial-and-error).
    /// </summary>
    public enum ActualDifficulty { Easy, Medium, Hard }

    public static class DifficultyAnalyzer
    {
        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Аналізує головоломку і повертає фактичну складність.</summary>
        public static ActualDifficulty Analyze(int[,] puzzle)
        {
            var grid = Clone(puzzle);

            bool usedHidden = false;
            bool usedAdvanced = false;

            while (HasEmpty(grid))
            {
                // 1. Naked singles — найпростіша техніка
                if (ApplyNakedSingles(grid))
                    continue; // після прогресу — знову з початку

                // 2. Hidden singles — середня техніка
                if (ApplyHiddenSingles(grid))
                {
                    usedHidden = true;
                    continue;
                }

                // 3. Нічого не допомогло → потрібні складні техніки
                usedAdvanced = true;
                break;
            }

            if (usedAdvanced) return ActualDifficulty.Hard;
            if (usedHidden) return ActualDifficulty.Medium;
            return ActualDifficulty.Easy;
        }

        // ── Naked Singles ──────────────────────────────────────────────────────

        /// <summary>
        /// Шукає клітинки рівно з одним кандидатом і ставить значення.
        /// Повертає true, якщо був хоча б один прогрес.
        /// </summary>
        private static bool ApplyNakedSingles(int[,] grid)
        {
            bool any = false;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                {
                    if (grid[r, c] != 0) continue;
                    var cands = Candidates(grid, r, c);
                    if (cands.Count == 1)
                    {
                        grid[r, c] = cands[0];
                        any = true;
                    }
                }
            return any;
        }

        // ── Hidden Singles ─────────────────────────────────────────────────────

        /// <summary>
        /// Для кожного рядка/стовпця/блоку шукає цифру,
        /// яка може стояти тільки в одній клітинці цієї секції.
        /// </summary>
        private static bool ApplyHiddenSingles(int[,] grid)
        {
            bool any = false;

            // Рядки
            for (int r = 0; r < 9; r++)
                for (int d = 1; d <= 9; d++)
                {
                    int pos = -1, count = 0;
                    for (int c = 0; c < 9; c++)
                        if (grid[r, c] == 0 && Candidates(grid, r, c).Contains(d))
                        { count++; pos = c; }

                    if (count == 1) { grid[r, pos] = d; any = true; }
                }

            // Стовпці
            for (int c = 0; c < 9; c++)
                for (int d = 1; d <= 9; d++)
                {
                    int pos = -1, count = 0;
                    for (int r = 0; r < 9; r++)
                        if (grid[r, c] == 0 && Candidates(grid, r, c).Contains(d))
                        { count++; pos = r; }

                    if (count == 1) { grid[pos, c] = d; any = true; }
                }

            // Блоки 3×3
            for (int br = 0; br < 3; br++)
                for (int bc = 0; bc < 3; bc++)
                    for (int d = 1; d <= 9; d++)
                    {
                        int pr = -1, pc = -1, count = 0;
                        for (int r = br * 3; r < br * 3 + 3; r++)
                            for (int c = bc * 3; c < bc * 3 + 3; c++)
                                if (grid[r, c] == 0 && Candidates(grid, r, c).Contains(d))
                                { count++; pr = r; pc = c; }

                        if (count == 1) { grid[pr, pc] = d; any = true; }
                    }

            return any;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static List<int> Candidates(int[,] grid, int row, int col)
        {
            var used = new bool[10];
            for (int c = 0; c < 9; c++) used[grid[row, c]] = true;
            for (int r = 0; r < 9; r++) used[grid[r, col]] = true;
            int sr = (row / 3) * 3, sc = (col / 3) * 3;
            for (int r = sr; r < sr + 3; r++)
                for (int c = sc; c < sc + 3; c++)
                    used[grid[r, c]] = true;

            var list = new List<int>();
            for (int d = 1; d <= 9; d++)
                if (!used[d]) list.Add(d);
            return list;
        }

        private static bool HasEmpty(int[,] g)
        {
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (g[r, c] == 0) return true;
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
    }
}