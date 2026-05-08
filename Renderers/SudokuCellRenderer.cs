using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SudokuApp.Models;

namespace SudokuApp.Renders
{
    public class SudokuCellRenderer : ICellRenderer
    {
        private readonly Brush _fixedForeground;
        private readonly Brush _userForeground;
        private readonly Brush _errorForeground;
        private readonly Brush _hintForeground;
        private readonly Brush _noteForeground;

        public SudokuCellRenderer(
            Brush fixedForeground,
            Brush userForeground,
            Brush errorForeground,
            Brush hintForeground,
            Brush noteForeground)
        {
            _fixedForeground = fixedForeground;
            _userForeground = userForeground;
            _errorForeground = errorForeground;
            _hintForeground = hintForeground;
            _noteForeground = noteForeground;
        }

        public void RenderCell(
            Cell cell,
            Border border,
            TextBlock mainText,
            Grid noteGrid,
            TextBlock[] noteTexts,
            bool isConflicting = false)
        {
            if (cell.Value != 0)
                RenderFilledCell(cell, mainText, noteGrid, isConflicting);
            else
                RenderEmptyCell(cell, mainText, noteGrid, noteTexts);
        }


        private void RenderFilledCell(
            Cell cell, TextBlock mainText, Grid noteGrid, bool isConflicting)
        {
            mainText.Text = cell.Value.ToString();
            mainText.Visibility = Visibility.Visible;
            noteGrid.Visibility = Visibility.Collapsed;

            mainText.Foreground =
                cell.IsFixed ? _fixedForeground :
                isConflicting ? _errorForeground :
                cell.IsHinted ? _hintForeground :
                                     _userForeground;

            mainText.FontWeight = cell.IsFixed ? FontWeights.Bold : FontWeights.SemiBold;
            mainText.FontSize = 22;
        }

        private void RenderEmptyCell(
            Cell cell, TextBlock mainText, Grid noteGrid, TextBlock[] noteTexts)
        {
            mainText.Text = "";

            for (int d = 0; d < 9; d++)
            {
                noteTexts[d].Text = cell.GetNote(d + 1) ? (d + 1).ToString() : "";
                noteTexts[d].Foreground = _noteForeground;
            }

            bool hasNotes = cell.HasNotes;
            mainText.Visibility = hasNotes ? Visibility.Collapsed : Visibility.Visible;
            noteGrid.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}