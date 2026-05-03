using System.Collections.Generic;

namespace SudokuApp.Models
{
    /// <summary>Holds the full state of a Sudoku game.</summary>
    public class SudokuBoard
    {
        public int[,] Grid { get; } = new int[9, 9];
        public bool[,] IsFixed { get; } = new bool[9, 9];
        public int[,] Solution { get; } = new int[9, 9];

        private readonly Stack<(int Row, int Col, int OldVal)> _history = new();

        // ─── Load ───────────────────────────────────────────────────────────

        public void LoadFromArrays(int[,] puzzle, int[,] solution)
        {
            _history.Clear();
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                {
                    Grid[r, c] = puzzle[r, c];
                    IsFixed[r, c] = puzzle[r, c] != 0;
                    Solution[r, c] = solution[r, c];
                }
        }

        // ─── Cell operations ────────────────────────────────────────────────

        /// <summary>Sets a cell value. Returns false if cell is fixed.</summary>
        public bool SetCell(int row, int col, int value, bool addToHistory = true)
        {
            if (IsFixed[row, col]) return false;
            if (addToHistory && Grid[row, col] != value)
                _history.Push((row, col, Grid[row, col]));
            Grid[row, col] = value;
            return true;
        }

        /// <summary>Undoes the last move. Returns false if nothing to undo.</summary>
        public bool Undo(out int row, out int col, out int restoredValue)
        {
            if (_history.Count == 0) { row = col = restoredValue = 0; return false; }
            var (r, c, v) = _history.Pop();
            Grid[r, c] = v;
            row = r; col = c; restoredValue = v;
            return true;
        }

        public void ClearHistory() => _history.Clear();

        /// <summary>Resets all non-fixed cells to 0.</summary>
        public void Reset()
        {
            _history.Clear();
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (!IsFixed[r, c]) Grid[r, c] = 0;
        }

        // ─── Validation ─────────────────────────────────────────────────────

        /// <summary>Checks whether placing <paramref name="value"/> at (row,col) violates any constraint.</summary>
        public bool IsValidMove(int row, int col, int value)
        {
            if (value == 0) return true;

            // Row
            for (int c = 0; c < 9; c++)
                if (c != col && Grid[row, c] == value) return false;

            // Column
            for (int r = 0; r < 9; r++)
                if (r != row && Grid[r, col] == value) return false;

            // 3×3 block
            int sr = (row / 3) * 3, sc = (col / 3) * 3;
            for (int r = sr; r < sr + 3; r++)
                for (int c = sc; c < sc + 3; c++)
                    if ((r != row || c != col) && Grid[r, c] == value) return false;

            return true;
        }

        /// <summary>True when every cell is filled.</summary>
        public bool IsComplete()
        {
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (Grid[r, c] == 0) return false;
            return true;
        }

        /// <summary>Returns a deep copy of the current grid values.</summary>
        public int[,] CloneGrid()
        {
            var g = new int[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    g[r, c] = Grid[r, c];
            return g;
        }
    }
}