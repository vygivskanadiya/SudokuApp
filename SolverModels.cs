namespace SudokuApp.Solvers
{
    // Відновлений список алгоритмів
    public enum SolverAlgorithm
    {
        Backtracking,
        ConstraintPropagation
    }

    /// <summary>
    /// Зберігає статистику процесу розв'язання.
    /// </summary>
    public struct SolveStats
    {
        public int ForwardSteps { get; }
        public int BacktrackSteps { get; } // Виправлено назву
        public long ElapsedMs { get; }     // Виправлено назву
        public bool TimedOut { get; }

        public SolveStats(int forwardSteps, int backtrackSteps, long elapsedMs, bool timedOut)
        {
            ForwardSteps = forwardSteps;
            BacktrackSteps = backtrackSteps;
            ElapsedMs = elapsedMs;
            TimedOut = timedOut;
        }
    }

    /// <summary>
    /// Описує один крок алгоритму для візуалізації.
    /// </summary>
    public struct SolveStep
    {
        public int Row { get; }
        public int Col { get; }
        public int Value { get; }
        public bool IsBacktrack { get; }

        public SolveStep(int row, int col, int value, bool IsBacktrack = false)
        {
            Row = row;
            Col = col;
            Value = value;
            this.IsBacktrack = IsBacktrack;
        }
    }
}
