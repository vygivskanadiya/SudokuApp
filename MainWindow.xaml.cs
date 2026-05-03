using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SudokuApp.Analyzers;
using SudokuApp.Generators;
using SudokuApp.Models;
using SudokuApp.Solvers;

namespace SudokuApp
{
    public partial class MainWindow : Window
    {
        // ═══════════════════════════════════════════════════════════
        //  Fields
        // ═══════════════════════════════════════════════════════════

        private readonly SudokuBoard _board = new();

        private readonly Border[,] _cells = new Border[9, 9];
        private readonly TextBlock[,] _cellTexts = new TextBlock[9, 9];
        private readonly Grid[,] _noteGrids = new Grid[9, 9];
        private readonly TextBlock[,][] _noteTexts = new TextBlock[9, 9][];

        private int _selRow = -1, _selCol = -1;
        private int _errorCount = 0;
        private int _hintsLeft = 3;
        private bool _notesMode = false;
        private bool _visualHelp = true;
        private bool _isRunning = false;

        private readonly bool[,,] _notes = new bool[9, 9, 9];

        // ── Статистика ходів ───────────────────────────────────────
        /// <summary>Скільки разів гравець/алгоритм заповнював клітинку (вперед).</summary>
        private int _playerMoves = 0;

        // ── Таймер ────────────────────────────────────────────────
        private readonly DispatcherTimer _timer = new();
        private int _elapsedSec = 0;

        // ── Покрокова анімація ─────────────────────────────────────
        private CancellationTokenSource? _stepCts;

        // ── Brushes ────────────────────────────────────────────────
        private static readonly SolidColorBrush BrNormal = Br("#FFFFFF");
        private static readonly SolidColorBrush BrHighlight = Br("#DCE9F8");
        private static readonly SolidColorBrush BrSelected = Br("#B8D4F5");
        private static readonly SolidColorBrush BrSameVal = Br("#BFDBFE");
        private static readonly SolidColorBrush BrFixed = Br("#1A1A2E");
        private static readonly SolidColorBrush BrUser = Br("#2563EB");
        private static readonly SolidColorBrush BrError = Br("#DC2626");
        private static readonly SolidColorBrush BrHint = Br("#059669");
        private static readonly SolidColorBrush BrStep = Br("#FEF08A");
        private static readonly SolidColorBrush BrBacktrack = Br("#FECACA");
        private static readonly SolidColorBrush BrNote = Br("#64748B");
        private static readonly SolidColorBrush BrCtrl = Br("#4B6080");

        private readonly Button[] _numPadButtons = new Button[9];

        private static SolidColorBrush Br(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(c);
        }


        // ═══════════════════════════════════════════════════════════
        //  Constructor
        // ═══════════════════════════════════════════════════════════

        public MainWindow()
        {
            InitializeComponent();
            BuildGrid();
            BuildNumberPad();
            SetupTimer();
            PreviewKeyDown += OnKeyDown;
            StartNewGame();
        }

        // ═══════════════════════════════════════════════════════════
        //  Grid construction
        // ═══════════════════════════════════════════════════════════

        private void UpdateNumberPadState()
        {
            int[] counts = new int[10];

            // Рахуємо кількість кожної цифри на дошці
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    int val = _board.Grid[r, c];
                    if (val > 0 && val <= 9) counts[val]++;
                }
            }

            // Вмикаємо/вимикаємо кнопки
            for (int d = 1; d <= 9; d++)
            {
                if (_numPadButtons[d - 1] != null)
                {
                    _numPadButtons[d - 1].IsEnabled = counts[d] < 9;
                    // Якщо хочеш, щоб вони зникали, використовуй це:
                    _numPadButtons[d - 1].Visibility = counts[d] < 9 ? Visibility.Visible : Visibility.Hidden;
                }
            }
        }

        private void BuildGrid()
        {
            for (int i = 0; i < 9; i++)
            {
                SudokuGrid.RowDefinitions.Add(new RowDefinition());
                SudokuGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (int row = 0; row < 9; row++)
                for (int col = 0; col < 9; col++)
                {
                    var cell = new Border
                    {
                        BorderBrush = Br("#6080A0"),
                        BorderThickness = CellBorder(row, col),
                        Background = BrNormal,
                        Cursor = Cursors.Hand,
                    };

                    var tb = new TextBlock
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 22,
                        FontFamily = new FontFamily("Segoe UI"),
                    };

                    var ng = new Grid { Margin = new Thickness(3) };
                    for (int i = 0; i < 3; i++)
                    {
                        ng.RowDefinitions.Add(new RowDefinition());
                        ng.ColumnDefinitions.Add(new ColumnDefinition());
                    }

                    _noteTexts[row, col] = new TextBlock[9];
                    for (int d = 0; d < 9; d++)
                    {
                        var nt = new TextBlock
                        {
                            FontSize = 9,
                            FontFamily = new FontFamily("Segoe UI"),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = BrNote,
                        };
                        Grid.SetRow(nt, d / 3);
                        Grid.SetColumn(nt, d % 3);
                        ng.Children.Add(nt);
                        _noteTexts[row, col][d] = nt;
                    }
                    ng.Visibility = Visibility.Collapsed;

                    var container = new Grid();
                    container.Children.Add(ng);
                    container.Children.Add(tb);
                    cell.Child = container;

                    int r = row, c = col;
                    cell.MouseLeftButtonDown += (_, _) => OnCellClick(r, c);

                    _cells[row, col] = cell;
                    _cellTexts[row, col] = tb;
                    _noteGrids[row, col] = ng;

                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    SudokuGrid.Children.Add(cell);
                }
        }

        private static Thickness CellBorder(int row, int col)
        {
            double left = col % 3 == 0 ? 2.5 : 0.5;
            double top = row % 3 == 0 ? 2.5 : 0.5;
            double right = col == 8 ? 2.5 : 0.0;
            double bottom = row == 8 ? 2.5 : 0.0;
            return new Thickness(left, top, right, bottom);
        }

        // ═══════════════════════════════════════════════════════════
        //  Number pad
        // ═══════════════════════════════════════════════════════════

        private void BuildNumberPad()
        {
            NumberPad.Children.Clear();
            for (int d = 1; d <= 9; d++)
            {
                int digit = d;
                var btn = new Button { Content = d.ToString(), Style = (Style)FindResource("NumBtn") };
                btn.Click += (_, _) => InputDigit(digit);
                NumberPad.Children.Add(btn);

                _numPadButtons[d - 1] = btn; // Зберігаємо кнопку в масив
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Timer
        // ═══════════════════════════════════════════════════════════

        private void SetupTimer()
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, _) =>
            {
                _elapsedSec++;
                int m = _elapsedSec / 60, s = _elapsedSec % 60;
                TxtTimer.Text = $"{m:D2}:{s:D2}";
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  New game
        // ═══════════════════════════════════════════════════════════

        // 1. Додаємо async
        private async void StartNewGame()
        {
            _stepCts?.Cancel();
            _isRunning = true; // Блокуємо дошку, поки йде генерація

            _errorCount = 0;
            _hintsLeft = 3;
            _elapsedSec = 0;
            _selRow = -1;
            _selCol = -1;
            _notesMode = false;
            _playerMoves = 0;

            Array.Clear(_notes, 0, _notes.Length);

            TxtErrors.Text = "0 / 3";
            TxtHints.Text = "3";
            TxtTimer.Text = "00:00";
            TxtNotesIcon.Foreground = BrCtrl;
            TxtNotesLabel.Text = "Примітки";
            TxtSolveIcon.Text = "⏳"; // Показуємо іконку завантаження
            TxtSolveLabel.Text = "Генерую...";

            var diff = DifficultyLabel.Tag is Difficulty d ? d : Difficulty.Easy;

            // 2. ВИНОСИМО ГЕНЕРАЦІЮ В ОКРЕМИЙ ПОТІК!
            var (puzzle, solution, actualDiff) = await Task.Run(() => SudokuGenerator.Generate(diff));

            _isRunning = false; // Розблоковуємо
            TxtSolveIcon.Text = "▶";
            TxtSolveLabel.Text = "Розв\u02bcязати";

            _board.LoadFromArrays(puzzle, solution);

            // Показати фактичну складність
            DifficultyLabel.Text = actualDiff switch
            {
                ActualDifficulty.Easy => "Легкий",
                ActualDifficulty.Medium => "Середній",
                ActualDifficulty.Hard => "Важкий",
                _ => "Легкий",
            };

            RefreshAll();
            _timer.Stop();
            _timer.Start();
        }

        // ═══════════════════════════════════════════════════════════
        //  Cell display
        // ═══════════════════════════════════════════════════════════

        private void RefreshAll()
        {
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    RefreshCell(r, c);
            RefreshHighlights();
            UpdateNumberPadState();
        }

        private void RefreshCell(int row, int col)
        {
            var tb = _cellTexts[row, col];
            var ng = _noteGrids[row, col];
            int val = _board.Grid[row, col];

            if (val != 0)
            {
                tb.Text = val.ToString();
                tb.Visibility = Visibility.Visible;
                ng.Visibility = Visibility.Collapsed;

                bool isFixed = _board.IsFixed[row, col];
                bool isInvalid = !_board.IsValidMove(row, col, val);

                tb.Foreground = isFixed ? BrFixed
                              : isInvalid ? BrError
                                          : BrUser;
                tb.FontWeight = isFixed ? FontWeights.Bold : FontWeights.SemiBold;
                tb.FontSize = 22;
            }
            else
            {
                tb.Text = "";
                bool hasNotes = false;
                for (int d = 0; d < 9; d++)
                {
                    _noteTexts[row, col][d].Text = _notes[row, col, d] ? (d + 1).ToString() : "";
                    if (_notes[row, col, d]) hasNotes = true;
                }
                tb.Visibility = hasNotes ? Visibility.Collapsed : Visibility.Visible;
                ng.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RefreshHighlights()
        {
            if (!_visualHelp)
            {
                for (int r = 0; r < 9; r++)
                    for (int c = 0; c < 9; c++)
                        _cells[r, c].Background = BrNormal;
                if (_selRow >= 0 && _selCol >= 0)
                    _cells[_selRow, _selCol].Background = BrSelected;
                return;
            }

            int selVal = (_selRow >= 0 && _selCol >= 0) ? _board.Grid[_selRow, _selCol] : 0;

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                {
                    bool isSel = r == _selRow && c == _selCol;
                    bool sameRow = _selRow >= 0 && r == _selRow;
                    bool sameCol = _selCol >= 0 && c == _selCol;
                    bool sameBlock = _selRow >= 0 && r / 3 == _selRow / 3 && c / 3 == _selCol / 3;
                    bool sameValue = selVal > 0 && _board.Grid[r, c] == selVal && !isSel;

                    _cells[r, c].Background =
                        isSel ? BrSelected :
                        sameValue ? BrSameVal :
                        (sameRow || sameCol || sameBlock) ? BrHighlight :
                        BrNormal;
                }
        }

        // ═══════════════════════════════════════════════════════════
        //  Input
        // ═══════════════════════════════════════════════════════════

        private void OnCellClick(int row, int col)
        {
            if (_isRunning) return;
            _selRow = row; _selCol = col;
            RefreshHighlights();
        }

        private void InputDigit(int digit)
        {
            if (_isRunning || _selRow < 0 || _selCol < 0) return;
            if (_board.IsFixed[_selRow, _selCol]) return;
            if (_errorCount >= 3) return;

            if (_notesMode)
            {
                if (_board.Grid[_selRow, _selCol] != 0) return;
                _notes[_selRow, _selCol, digit - 1] ^= true;
                RefreshCell(_selRow, _selCol);
                return;
            }

            _board.SetCell(_selRow, _selCol, digit);
            for (int d = 0; d < 9; d++) _notes[_selRow, _selCol, d] = false;

            if (digit != 0)
            {
                _playerMoves++;

                if (!_board.IsValidMove(_selRow, _selCol, digit))
                {
                    _errorCount++;
                    TxtErrors.Text = $"{_errorCount} / 3";

                    if (_errorCount >= 3)
                    {
                        _timer.Stop();
                        RefreshCell(_selRow, _selCol);
                        RefreshHighlights();
                        ShowGameOver();
                        return;
                    }
                }
            }

            RefreshCell(_selRow, _selCol);
            RefreshHighlights();
            UpdateNumberPadState();
            CheckWin();
        }

        private void ClearCurrentCell()
        {
            if (_selRow < 0 || _selCol < 0) return;
            if (_board.IsFixed[_selRow, _selCol]) return;
            _board.SetCell(_selRow, _selCol, 0);
            for (int d = 0; d < 9; d++) _notes[_selRow, _selCol, d] = false;
            RefreshCell(_selRow, _selCol);
            RefreshHighlights();
            UpdateNumberPadState();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key >= Key.D1 && e.Key <= Key.D9) { InputDigit(e.Key - Key.D0); e.Handled = true; }
            else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9) { InputDigit(e.Key - Key.NumPad0); e.Handled = true; }
            else if (e.Key is Key.Delete or Key.Back or Key.D0 or Key.NumPad0) { ClearCurrentCell(); e.Handled = true; }
            else if (e.Key == Key.Up && _selRow > 0) { _selRow--; RefreshHighlights(); e.Handled = true; }
            else if (e.Key == Key.Down && _selRow < 8) { _selRow++; RefreshHighlights(); e.Handled = true; }
            else if (e.Key == Key.Left && _selCol > 0) { _selCol--; RefreshHighlights(); e.Handled = true; }
            else if (e.Key == Key.Right && _selCol < 8) { _selCol++; RefreshHighlights(); e.Handled = true; }
        }

        // ═══════════════════════════════════════════════════════════
        //  Win / Lose
        // ═══════════════════════════════════════════════════════════

        private void CheckWin()
        {
            if (!_board.IsComplete()) return;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (!_board.IsValidMove(r, c, _board.Grid[r, c])) return;

            _timer.Stop();
            int m = _elapsedSec / 60, s = _elapsedSec % 60;

            MessageBox.Show(
                $"Вітаємо! Судоку розв\u02bcязано!\n\n" +
                $"Час: {m:D2}:{s:D2}\n" +
                $"Заповнено клітинок:  {_playerMoves}\n" +
                $"Помилок:       {_errorCount}",
                "Перемога!", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowGameOver()
        {
            int m = _elapsedSec / 60, s = _elapsedSec % 60;
            MessageBox.Show(
                $"Ви допустили 3 помилки. Гру завершено!\n\n" +
                $"Час: {m:D2}:{s:D2}\n" +
                $"Заповнено клітинок:   {_playerMoves}\n" +
                $"Помилок:        {_errorCount}",
                "Гра закінчена", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ═══════════════════════════════════════════════════════════
        //  Button handlers
        // ═══════════════════════════════════════════════════════════

        private void NewGame_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new DifficultyDialog { Owner = this };
            if (dlg.ShowDialog() == true)
                DifficultyLabel.Tag = dlg.SelectedDifficulty;
            StartNewGame();
        }

        private void VisualHelp_Click(object sender, RoutedEventArgs e)
        {
            _visualHelp = !_visualHelp;

            // Текст можеш залишити однаковий, або додати (ВКЛ/ВИМК) для зрозумілості
            BtnVisualHelp.Content = "Підсвітка";

            // Якщо _visualHelp == true (увімкнено), ставимо темний стиль. Якщо false - світлий.
            BtnVisualHelp.Style = (Style)FindResource(_visualHelp ? "DarkPinkHoverBtn" : "PinkHoverBtn");

            RefreshHighlights();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            if (_board.Undo(out int row, out int col, out _))
            {
                // відкат — не рахуємо як хід
                _selRow = row; _selCol = col;
                RefreshCell(row, col);
                RefreshHighlights();
                UpdateNumberPadState();
            }
        }

        private void ClearCell_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            ClearCurrentCell();
        }

        private void Notes_Click(object sender, RoutedEventArgs e)
        {
            _notesMode = !_notesMode;
            TxtNotesIcon.Foreground = _notesMode ? BrUser : BrCtrl;
            TxtNotesLabel.Text = _notesMode ? "Примітки: ВКЛ" : "Примітки";
        }

        private void Hint_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning || _hintsLeft <= 0) return;

            if (_selRow < 0 || _selCol < 0 || _board.IsFixed[_selRow, _selCol]
                || _board.Grid[_selRow, _selCol] == _board.Solution[_selRow, _selCol])
            {
                bool found = false;
                for (int r = 0; r < 9 && !found; r++)
                    for (int c = 0; c < 9 && !found; c++)
                        if (!_board.IsFixed[r, c] && _board.Grid[r, c] != _board.Solution[r, c])
                        { _selRow = r; _selCol = c; found = true; }
                if (!found) return;
            }

            int sol = _board.Solution[_selRow, _selCol];
            _board.SetCell(_selRow, _selCol, sol, addToHistory: false);
            for (int d = 0; d < 9; d++) _notes[_selRow, _selCol, d] = false;
            // підказка — не рахуємо в playerMoves

            RefreshCell(_selRow, _selCol);
            _cellTexts[_selRow, _selCol].Foreground = BrHint;

            _hintsLeft--;
            TxtHints.Text = _hintsLeft.ToString();
            RefreshHighlights();
            UpdateNumberPadState();
            CheckWin();
        }

        private async void Solve_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                _stepCts?.Cancel();
                return;
            }

            var alg = RadioBacktracking.IsChecked == true
                ? SolverAlgorithm.Backtracking
                : SolverAlgorithm.ConstraintPropagation;

            if (RadioStep.IsChecked == true)
                await RunStepModeAsync(alg);
            else
                await RunAutoModeAsync(alg);
        }

        // ═══════════════════════════════════════════════════════════
        //  Solver modes
        // ═══════════════════════════════════════════════════════════

        /// <summary>Автоматичний режим — розв'язання за 30 с або помилка.</summary>
        private async Task RunAutoModeAsync(SolverAlgorithm alg)
        {
            _isRunning = true;
            TxtSolveIcon.Text = "■";
            TxtSolveLabel.Text = "Зупинити";

            var grid = _board.CloneGrid();

            SolveStats stats = await Task.Run(() => SudokuSolver.Solve(grid, alg));

            _isRunning = false;
            TxtSolveIcon.Text = "▶";
            TxtSolveLabel.Text = "Розв\u02bcязати";

            if (stats.TimedOut)
            {
                ShowTimeout(alg, stats);
                return;
            }

            // Перевірити чи є рішення
            bool hasSolution = false;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (grid[r, c] == 0) { hasSolution = false; goto done; }
            hasSolution = true;
        done:

            if (!hasSolution)
            {
                MessageBox.Show(
                    "Не вдалося знайти рішення для поточного стану дошки.\n" +
                    "Можливо, деякі введені вами числа конфліктують між собою.",
                    "Рішення не знайдено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (!_board.IsFixed[r, c])
                        _board.SetCell(r, c, grid[r, c], addToHistory: false);

            RefreshAll();
            _timer.Stop();

            string algName = alg == SolverAlgorithm.Backtracking ? "Backtracking" : "CSP";
            MessageBox.Show(
                $"Судоку розв\u02bcязано автоматично!\n\n" +
                $"Алгоритм:         {algName}\n" +
                $"Час виконання:    {stats.ElapsedMs} мс\n" +
                $"Кроків вперед:   {stats.ForwardSteps}\n" +
                $"Відкатів:         {stats.BacktrackSteps}\n" +
                $"Всього ходів:     {stats.ForwardSteps + stats.BacktrackSteps}",
                "Готово!", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>Покроковий режим із 30-секундним таймаутом.</summary>
        private async Task RunStepModeAsync(SolverAlgorithm alg)
        {
            var startGrid = new int[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    startGrid[r, c] = _board.IsFixed[r, c] ? _board.Grid[r, c] : 0;

            // Обчислення кроків у фоновому потоці (з таймаутом 30 с)
            _stepCts = new CancellationTokenSource();
            var (steps, stats) = await Task.Run(
                () => SudokuSolver.GetSteps(startGrid, alg, _stepCts.Token));

            if (stats.TimedOut || steps == null)
            {
                _stepCts = null;
                ShowTimeout(alg, stats);
                return;
            }

            // Скинути поле до початкового стану
            _board.Reset();
            RefreshAll();

            _isRunning = true;
            TxtSolveIcon.Text = "■";
            TxtSolveLabel.Text = "Стоп";

            // Новий CTS лише для зупинки анімації
            _stepCts = new CancellationTokenSource();
            var ct = _stepCts.Token;

            try
            {
                foreach (var step in steps)
                {
                    ct.ThrowIfCancellationRequested();

                    await Dispatcher.InvokeAsync(() =>
                    {
                        _board.SetCell(step.Row, step.Col, step.Value, addToHistory: false);
                        RefreshCell(step.Row, step.Col);
                        _cells[step.Row, step.Col].Background =
                            step.IsBacktrack ? BrBacktrack : BrStep;
                    });

                    await Task.Delay(step.IsBacktrack ? 30 : 60, ct);
                    await Dispatcher.InvokeAsync(RefreshHighlights);
                }

                _timer.Stop();
                string algName = alg == SolverAlgorithm.Backtracking ? "Backtracking" : "CSP";
                MessageBox.Show(
                    $"Алгоритм завершив розв\u02bcязання!\n\n" +
                    $"Алгоритм:         {algName}\n" +
                    $"Обчислення:        {stats.ElapsedMs} мс\n" +
                    $"Кроків вперед:   {stats.ForwardSteps}\n" +
                    $"Відкатів:         {stats.BacktrackSteps}\n" +
                    $"Всього ходів:     {stats.ForwardSteps + stats.BacktrackSteps}",
                    "Покроковий режим", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException) { /* зупинено користувачем */ }
            finally
            {
                _isRunning = false;
                TxtSolveIcon.Text = "▶";
                TxtSolveLabel.Text = "Розв\u02bcязати";
                _stepCts = null;
                RefreshAll();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Timeout message
        // ═══════════════════════════════════════════════════════════

        private static void ShowTimeout(SolverAlgorithm alg, SolveStats stats)
        {
            string algName = alg == SolverAlgorithm.Backtracking ? "Backtracking" : "CSP";
            string reason = alg == SolverAlgorithm.Backtracking
                ? "Алгоритм Backtracking у найгіршому випадку має складність O(9ⁿ). " +
                  "На дуже важких або некоректних головоломках він може перебирати " +
                  "мільярди комбінацій. Спробуйте скинути поле або обрати алгоритм CSP."
                : "Алгоритм CSP не зміг знайти рішення за допустимий час. " +
                  "Можливо, введені вами числа створюють суперечність у головоломці. " +
                  "Перевірте коректність поля або почніть нову гру.";

            MessageBox.Show(
                $"Перевищено ліміт часу (2 хвилини)!\n\n" +
                $"Алгоритм: {algName}\n" +
                $"Кроків до зупинки:  {stats.ForwardSteps}\n" +
                $"Відкатів до зупинки: {stats.BacktrackSteps}\n\n" +
                $"Причина:\n{reason}",
                "Таймаут алгоритму", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Difficulty dialog
    // ═══════════════════════════════════════════════════════════════

    internal class DifficultyDialog : Window
    {
        public Difficulty SelectedDifficulty { get; private set; } = Difficulty.Easy;

        public DifficultyDialog()
        {
            Title = "Вибір складності";
            Width = 320; Height = 230;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF0FB"));
            FontFamily = new FontFamily("Segoe UI");

            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            panel.Children.Add(new TextBlock
            {
                Text = "Оберіть рівень складності:",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A2E")),
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Легкий — лише одиночні кандидати\nСередній — потрібні приховані одиночки\nВажкий — потрібен перебір",
                FontSize = 11.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                Margin = new Thickness(0, 0, 0, 14)
            });

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };

            (string Label, string Color, Difficulty Diff)[] items =
            {
                ("Легкий",   "#059669", Difficulty.Easy),
                ("Середній", "#D97706", Difficulty.Medium),
                ("Важкий",   "#DC2626", Difficulty.Hard),
            };

            foreach (var (label, color, diff) in items)
            {
                var c = (Color)ColorConverter.ConvertFromString(color);
                var btn = new Button
                {
                    Content = label,
                    Width = 80,
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(0, 8, 0, 8),
                    Background = new SolidColorBrush(c),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                };

                var tmpl = new ControlTemplate(typeof(Button));
                var factory = new FrameworkElementFactory(typeof(Border));
                factory.SetBinding(Border.BackgroundProperty,
                    new System.Windows.Data.Binding("Background")
                    {
                        RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
                    });
                factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                factory.SetBinding(Border.PaddingProperty,
                    new System.Windows.Data.Binding("Padding")
                    {
                        RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
                    });
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                factory.AppendChild(cp);
                tmpl.VisualTree = factory;
                btn.Template = tmpl;

                var d = diff;
                btn.Click += (_, _) => { SelectedDifficulty = d; DialogResult = true; };
                btnPanel.Children.Add(btn);
            }

            panel.Children.Add(btnPanel);
            Content = panel;
        }
    }
}
