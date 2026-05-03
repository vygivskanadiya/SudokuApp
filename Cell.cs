using System;

namespace SudokuApp.Models
{
    /// <summary>
    /// Визначає поточний візуальний стан клітинки для рендерингу.
    /// </summary>
    public enum CellVisualState
    {
        Normal,
        Selected,
        Highlighted,
        SameValue,
        AnimationStep,
        AnimationBacktrack
    }

    /// <summary>
    /// Представляє одну клітинку судоку.
    /// Інкапсулює: значення, фіксованість, рішення, нотатки, візуальний стан.
    /// Принцип єдиної відповідальності (SRP): клітинка відповідає лише за свій стан.
    /// Принцип інкапсуляції (OOP): внутрішній стан змінюється лише через публічні методи.
    /// </summary>
    public class Cell
    {
        // ── Внутрішнє зберігання ──────────────────────────────────────────
        private readonly bool[] _notes = new bool[9];

        // ── Публічні властивості ──────────────────────────────────────────

        /// <summary>Поточне значення клітинки (0 = порожня).</summary>
        public int Value { get; private set; }

        /// <summary>True, якщо клітинка є початковою (незмінною) частиною задачі.</summary>
        public bool IsFixed { get; private set; }

        /// <summary>Правильне рішення для цієї клітинки.</summary>
        public int SolutionValue { get; private set; }

        /// <summary>True, якщо значення встановлено за допомогою підказки.</summary>
        public bool IsHinted { get; private set; }

        /// <summary>Поточний візуальний стан для рендерингу (підсвітка тощо).</summary>
        public CellVisualState VisualState { get; set; } = CellVisualState.Normal;

        /// <summary>True, якщо клітинка містить хоча б одну нотатку.</summary>
        public bool HasNotes
        {
            get
            {
                for (int i = 0; i < 9; i++)
                    if (_notes[i]) return true;
                return false;
            }
        }

        // ── Доступ до нотаток ─────────────────────────────────────────────

        /// <summary>Повертає стан нотатки для цифри 1–9.</summary>
        public bool GetNote(int digit) =>
            digit >= 1 && digit <= 9 && _notes[digit - 1];

        // ── Ініціалізація ─────────────────────────────────────────────────

        /// <summary>
        /// Ініціалізує клітинку при завантаженні нової головоломки.
        /// Викликається лише з SudokuBoard.
        /// </summary>
        internal void Initialize(int puzzleValue, int solutionValue)
        {
            Value = puzzleValue;
            IsFixed = puzzleValue != 0;
            SolutionValue = solutionValue;
            IsHinted = false;
            ClearNotes();
            VisualState = CellVisualState.Normal;
        }

        // ── Мутація ───────────────────────────────────────────────────────

        /// <summary>
        /// Встановлює значення клітинки.
        /// </summary>
        /// <param name="value">Значення 1–9 або 0 (очистити).</param>
        /// <param name="asHint">True — значення встановлено як підказка.</param>
        /// <returns>False, якщо клітинка є фіксованою.</returns>
        public bool SetValue(int value, bool asHint = false)
        {
            if (IsFixed) return false;
            Value = value;
            if (asHint) IsHinted = true;
            if (value != 0) ClearNotes();
            return true;
        }

        /// <summary>Перемикає нотатку для цифри 1–9.</summary>
        public void ToggleNote(int digit)
        {
            if (digit < 1 || digit > 9 || Value != 0) return;
            _notes[digit - 1] = !_notes[digit - 1];
        }

        /// <summary>Очищає всі нотатки клітинки.</summary>
        public void ClearNotes() => Array.Clear(_notes, 0, 9);

        /// <summary>Скидає нефіксовану клітинку до порожнього стану.</summary>
        public void Reset()
        {
            if (IsFixed) return;
            Value = 0;
            IsHinted = false;
            ClearNotes();
            VisualState = CellVisualState.Normal;
        }
    }
}
