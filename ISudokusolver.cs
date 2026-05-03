using System.Collections.Generic;
using System.Threading;

namespace SudokuApp.Solvers
{
    /// <summary>
    /// Контракт для алгоритму розв'язання судоку.
    /// Завдяки інтерфейсу MainWindow не залежить від конкретного алгоритму —
    /// принцип Dependency Inversion (SOLID).
    /// </summary>
    public interface ISudokuSolver
    {
        /// <summary>
        /// Розв'язує grid in-place. Повертає статистику.
        /// ct дозволяє зупинити ззовні (кнопка Стоп, таймаут).
        /// </summary>
        SolveStats Solve(int[,] grid, CancellationToken ct = default);

        /// <summary>
        /// Записує кожну зміну для покрокової анімації.
        /// Steps == null якщо скасовано або рішення не знайдено.
        /// </summary>
        (List<SolveStep>? Steps, SolveStats Stats) GetSteps(
            int[,] grid, CancellationToken ct = default);
    }
}
