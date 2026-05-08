namespace SudokuApp.Solvers
{
    public enum SolverAlgorithm
    {
        Backtracking,
        ConstraintPropagation
    }

    public struct SolveStats
    {
        public int ForwardSteps { get; }
        public int BacktrackSteps { get; }
        public long ElapsedMs { get; }
        public bool TimedOut { get; }

        public SolveStats(int forwardSteps, int backtrackSteps, long elapsedMs, bool timedOut)
        {
            ForwardSteps = forwardSteps;
            BacktrackSteps = backtrackSteps;
            ElapsedMs = elapsedMs;
            TimedOut = timedOut;
        }
    }

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
