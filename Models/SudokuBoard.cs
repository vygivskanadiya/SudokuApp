using System.Collections.Generic;

namespace SudokuApp.Models
{
    /// <summary>
    /// Зберігає та керує повним станом ігрової дошки судоку через масив Cell.
    /// Відповідає за: завантаження головоломок, операції з клітинками, валідацію, undo.
    /// Принцип SRP: дошка відповідає лише за стан і правила гри, не за UI.
    /// Принцип інкапсуляції: зовнішній код отримує доступ через Cell-об'єкти,
    /// а не через сирі масиви int[,] / bool[,].
    /// </summary>
    public class SudokuBoard
    {
        // ── Клітинки ──────────────────────────────────────────────────────
        private readonly Cell[,] _cells;
        private readonly Stack<(int Row, int Col, int OldVal)> _history = new();

        // ── Конструктор ───────────────────────────────────────────────────
        public SudokuBoard()
        {
            _cells = new Cell[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    _cells[r, c] = new Cell();
        }

        // ── Доступ до клітинок ────────────────────────────────────────────

        /// <summary>Індексатор: зручний доступ до клітинки по координатах.</summary>
        public Cell this[int row, int col] => _cells[row, col];

        // ── Завантаження ──────────────────────────────────────────────────

        /// <summary>Завантажує нову головоломку та рішення у дошку.</summary>
        public void LoadFromArrays(int[,] puzzle, int[,] solution)
        {
            _history.Clear();
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    _cells[r, c].Initialize(puzzle[r, c], solution[r, c]);
        }

        // ── Операції з клітинками ─────────────────────────────────────────

        /// <summary>
        /// Встановлює значення клітинки.
        /// </summary>
        /// <returns>False, якщо клітинка є фіксованою.</returns>
        public bool SetCell(int row, int col, int value, bool addToHistory = true)
        {
            var cell = _cells[row, col];
            if (cell.IsFixed) return false;
            if (addToHistory && cell.Value != value)
                _history.Push((row, col, cell.Value));
            cell.SetValue(value);
            return true;
        }

        /// <summary>
        /// Встановлює значення клітинки як підказку (позначає IsHinted = true).
        /// Не додає до історії скасування.
        /// </summary>
        public bool SetCellHint(int row, int col, int value)
        {
            var cell = _cells[row, col];
            if (cell.IsFixed) return false;
            cell.SetValue(value, asHint: true);
            return true;
        }

        /// <summary>Скасовує останній хід. Повертає false, якщо нічого скасовувати.</summary>
        public bool Undo(out int row, out int col, out int restoredValue)
        {
            if (_history.Count == 0) { row = col = restoredValue = 0; return false; }
            var (r, c, v) = _history.Pop();
            _cells[r, c].SetValue(v);
            row = r; col = c; restoredValue = v;
            return true;
        }

        public void ClearHistory() => _history.Clear();

        /// <summary>Скидає всі нефіксовані клітинки до 0.</summary>
        public void Reset()
        {
            _history.Clear();
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    _cells[r, c].Reset();
        }

        // ── Валідація ─────────────────────────────────────────────────────

        /// <summary>Перевіряє, чи значення у (row,col) конфліктує з правилами судоку.</summary>
        public bool IsValidMove(int row, int col, int value)
        {
            if (value == 0) return true;

            for (int c = 0; c < 9; c++)
                if (c != col && _cells[row, c].Value == value) return false;

            for (int r = 0; r < 9; r++)
                if (r != row && _cells[r, col].Value == value) return false;

            int sr = (row / 3) * 3, sc = (col / 3) * 3;
            for (int r = sr; r < sr + 3; r++)
                for (int c = sc; c < sc + 3; c++)
                    if ((r != row || c != col) && _cells[r, c].Value == value) return false;

            return true;
        }

        /// <summary>True, коли всі клітинки заповнені.</summary>
        public bool IsComplete()
        {
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (_cells[r, c].Value == 0) return false;
            return true;
        }

        /// <summary>
        /// Повертає глибоку копію поточних значень у вигляді int[,]
        /// (для передачі в алгоритми-солвери).
        /// </summary>
        public int[,] CloneGrid()
        {
            var g = new int[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    g[r, c] = _cells[r, c].Value;
            return g;
        }
    }
}