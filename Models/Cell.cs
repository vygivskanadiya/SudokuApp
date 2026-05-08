using System;

namespace SudokuApp.Models
{

    public enum CellVisualState
    {
        Normal,
        Selected,
        Highlighted,
        SameValue,
        AnimationStep,
        AnimationBacktrack
    }

    public class Cell
    {
        private readonly bool[] _notes = new bool[9];

        public int Value { get; private set; }

        public bool IsFixed { get; private set; }

        public int SolutionValue { get; private set; }

        public bool IsHinted { get; private set; }

        public CellVisualState VisualState { get; set; } = CellVisualState.Normal;

        public bool HasNotes
        {
            get
            {
                for (int i = 0; i < 9; i++)
                    if (_notes[i]) return true;
                return false;
            }
        }

        public bool GetNote(int digit) =>
            digit >= 1 && digit <= 9 && _notes[digit - 1];

        internal void Initialize(int puzzleValue, int solutionValue)
        {
            Value = puzzleValue;
            IsFixed = puzzleValue != 0;
            SolutionValue = solutionValue;
            IsHinted = false;
            ClearNotes();
            VisualState = CellVisualState.Normal;
        }

        public bool SetValue(int value, bool asHint = false)
        {
            if (IsFixed) return false;
            Value = value;
            if (asHint) IsHinted = true;
            if (value != 0) ClearNotes();
            return true;
        }

        public void ToggleNote(int digit)
        {
            if (digit < 1 || digit > 9 || Value != 0) return;
            _notes[digit - 1] = !_notes[digit - 1];
        }

        public void ClearNotes() => Array.Clear(_notes, 0, 9);

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
