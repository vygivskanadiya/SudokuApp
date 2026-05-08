using System;

namespace SudokuApp.Solvers
{
    public static class SudokuSolverFactory
    {
        public static ISudokuSolver Create(SolverAlgorithm algorithm) => algorithm switch
        {
            SolverAlgorithm.Backtracking => new BacktrackingSolver(),
            SolverAlgorithm.ConstraintPropagation => new CspSolver(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
    }
}
