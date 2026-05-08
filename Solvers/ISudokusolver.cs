using System.Collections.Generic;
using System.Threading;

namespace SudokuApp.Solvers
{

    public interface ISudokuSolver
    {

        SolveStats Solve(int[,] grid, CancellationToken ct = default);

        (List<SolveStep>? Steps, SolveStats Stats) GetSteps(
            int[,] grid, CancellationToken ct = default);
    }
}
