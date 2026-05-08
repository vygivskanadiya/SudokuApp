using System.Windows.Controls;
using System.Windows.Media;
using SudokuApp.Models;

namespace SudokuApp.Renders
{
    public interface ICellRenderer
    {
        void RenderCell(
            Cell cell,
            Border border,
            TextBlock mainText,
            Grid noteGrid,
            TextBlock[] noteTexts,
            bool isConflicting = false);
    }
}