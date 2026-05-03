using System;

namespace SudokuApp.Solvers
{
    /// <summary>
    /// Фабрика — єдине місце, де enum перетворюється на конкретний клас.
    /// Увесь інший код працює тільки з <see cref="ISudokuSolver"/>.
    /// </summary>
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
