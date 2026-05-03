using System.Windows.Controls;
using System.Windows.Media;
using SudokuApp.Models;

namespace SudokuApp.Renders
{
    /// <summary>
    /// Контракт для відображення клітинки судоку в UI.
    /// Принцип DIP (Dependency Inversion): MainWindow залежить від цієї абстракції,
    /// а не від конкретного класу SudokuCellRenderer.
    /// Принцип ISP (Interface Segregation): інтерфейс містить лише методи,
    /// які реально потрібні клієнту.
    /// </summary>
    public interface ICellRenderer
    {
        /// <summary>
        /// Відображає вміст клітинки (значення або нотатки) у переданих елементах UI.
        /// </summary>
        /// <param name="cell">Дані клітинки для відображення.</param>
        /// <param name="border">Зовнішній контейнер клітинки.</param>
        /// <param name="mainText">TextBlock для числового значення.</param>
        /// <param name="noteGrid">Grid для нотаток (pencil marks).</param>
        /// <param name="noteTexts">9 TextBlock'ів для цифр 1–9 у нотатках.</param>
        /// <param name="isConflicting">True, якщо значення порушує правила судоку.</param>
        void RenderCell(
            Cell cell,
            Border border,
            TextBlock mainText,
            Grid noteGrid,
            TextBlock[] noteTexts,
            bool isConflicting = false);
    }
}