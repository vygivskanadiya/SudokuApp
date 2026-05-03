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
using SudokuApp.Game;
using SudokuApp.Generators;
using SudokuApp.Models;
using SudokuApp.Rendering;
using SudokuApp.Solvers;

namespace SudokuApp
{
    /// <summary>
    /// Головне вікно програми.
    /// Принцип SRP: UI-логіка делегована допоміжним класам:
    ///   • SudokuBoard    — стан дошки та клітинок
    ///   • GameStateManager — помилки, підказки, ходи
    ///   • SudokuCellRenderer (ICellRenderer) — відображення клітинок
    /// MainWindow відповідає лише за координацію UI-подій.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ═══════════════════════════════════════════════════════════
        //  Залежності (OOP: слабке зв'язування через інтерфейс)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Стан ігрової дошки (клітинки, правила, undo).</summary>
        private readonly SudokuBoard _board = new();

        /// <summary>
        /// Стан ігрової сесії: помилки, підказки, ходи.
        /// Принцип DIP: MainWindow залежить від конкретного класу,
        /// але через чіткий публічний контракт (не внутрішні деталі).
        /// </summary>
        private readonly GameStateManager _gameState = new();

        /// <summary>
        /// Рендерер клітинок.
        /// Принцип DIP: MainWindow залежить від ICellRenderer (абстракції),
        /// а не від SudokuCellRenderer (реалізації).
        /// </summary>
        private readonly ICellRenderer _cellRenderer;

        // ═══════════════════════════════════════════════════════════
        //  UI-контроли сітки (масиви по клітинках)
        // ═══════════════════════════════════════════════════════════

        private readonly Border[,] _cellBorders = new Border[9, 9];
        private readonly TextBlock[,] _cellTexts = new TextBlock[9, 9];
        private readonly Grid[,] _noteGrids = new Grid[9, 9];
        private readonly TextBlock[,][] _noteTexts = new TextBlock[9, 9][];

        // ═══════════════════════════════════════════════════════════
        //  Стан вікна
        // ═══════════════════════════════════════════════════════════

        private int _selRow = -1, _selCol = -1;
        private bool _notesMode = false;
        private bool _visualHelp = true;
        private bool _isRunning = false;
        private bool _manualCancel = false;

        // ── Таймер ────────────────────────────────────────────────
        private readonly DispatcherTimer _timer = new();
        private int _elapsedSec = 0;

        // ── CancellationToken ─────────────────────────────────────
        private CancellationTokenSource? _cts;
        private const int TimeoutSeconds = 120;

        // ── Цифрова клавіатура ────────────────────────────────────
        private readonly Button[] _numPadButtons = new Button[9];

        // ═══════════════════════════════════════════════════════════
        //  Кольорова схема (пензлі)
        // ═══════════════════════════════════════════════════════════

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

        private static SolidColorBrush Br(string hex) =>
            new((Color)ColorConverter.ConvertFromString(hex));

        // ═══════════════════════════════════════════════════════════
        //  Конструктор
        // ═══════════════════════════════════════════════════════════

        public MainWindow()
        {
            InitializeComponent();

            // Створення рендерера через конструктор (Dependency Injection).
            // MainWindow не знає про внутрішню реалізацію SudokuCellRenderer.
            _cellRenderer = new SudokuCellRenderer(
                fixedForeground: BrFixed,
                userForeground: BrUser,
                errorForeground: BrError,
                hintForeground: BrHint,
                noteForeground: BrNote);

            BuildGrid();
            BuildNumberPad();
            SetupTimer();

            PreviewKeyDown += OnKeyDown;
        }

        // ═══════════════════════════════════════════════════════════
        //  Навігація між екранами
        // ═══════════════════════════════════════════════════════════

        private void MenuStart_Click(object sender, RoutedEventArgs e)
            => ShowPanel(PanelDifficulty);

        private void BackToMenu_Click(object sender, RoutedEventArgs e)
            => ShowPanel(PanelMenu);

        private void DiffEasy_Click(object sender, RoutedEventArgs e) => LaunchGame(Difficulty.Easy);
        private void DiffMedium_Click(object sender, RoutedEventArgs e) => LaunchGame(Difficulty.Medium);
        private void DiffHard_Click(object sender, RoutedEventArgs e) => LaunchGame(Difficulty.Hard);

        private void LaunchGame(Difficulty diff)
        {
            DifficultyLabel.Tag = diff;
            ShowPanel(PanelGame);
            StartNewGame();
        }

        private void GoToMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                var r = MessageBox.Show(
                    "Алгоритм ще працює. Зупинити і вийти в меню?",
                    "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
                _cts?.Cancel();
                _isRunning = false;
            }
            _timer.Stop();
            ShowPanel(PanelMenu);
        }

        private void ShowPanel(UIElement panel)
        {
            PanelMenu.Visibility = Visibility.Collapsed;
            PanelDifficulty.Visibility = Visibility.Collapsed;
            PanelGame.Visibility = Visibility.Collapsed;
            panel.Visibility = Visibility.Visible;
        }

        // ═══════════════════════════════════════════════════════════
        //  Правила гри (кнопка-перемикач)
        // ═══════════════════════════════════════════════════════════

        private void RulesToggle_Click(object sender, RoutedEventArgs e)
        {
            bool expanded = RulesPanel.Visibility == Visibility.Visible;
            RulesPanel.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
            BtnRules.Content = expanded ? "📋  Правила гри  ▼" : "📋  Правила гри  ▲";
        }

        // ═══════════════════════════════════════════════════════════
        //  Побудова сітки
        // ═══════════════════════════════════════════════════════════

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

                    _cellBorders[row, col] = cell;
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
        //  Цифрова клавіатура
        // ═══════════════════════════════════════════════════════════

        private void BuildNumberPad()
        {
            NumberPad.Children.Clear();
            for (int d = 1; d <= 9; d++)
            {
                int digit = d;
                var btn = new Button
                {
                    Content = d.ToString(),
                    Style = (Style)FindResource("NumBtn")
                };
                btn.Click += (_, _) => InputDigit(digit);
                NumberPad.Children.Add(btn);
                _numPadButtons[d - 1] = btn;
            }
        }

        private void UpdateNumberPadState()
        {
            int[] counts = new int[10];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                {
                    int val = _board[r, c].Value;
                    if (val > 0 && val <= 9) counts[val]++;
                }
            for (int d = 1; d <= 9; d++)
                if (_numPadButtons[d - 1] != null)
                {
                    _numPadButtons[d - 1].IsEnabled = counts[d] < 9;
                    _numPadButtons[d - 1].Visibility =
                        counts[d] < 9 ? Visibility.Visible : Visibility.Hidden;
                }
        }

        // ═══════════════════════════════════════════════════════════
        //  Таймер
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
        //  Нова гра
        // ═══════════════════════════════════════════════════════════

        private void StartNewGame()
        {
            _cts?.Cancel();
            _isRunning = false;

            // Скидаємо стан сесії через GameStateManager (SRP)
            _gameState.Reset();

            _elapsedSec = 0;
            _selRow = -1;
            _selCol = -1;
            _notesMode = false;

            TxtErrors.Text = "0 / 3";
            TxtHints.Text = GameStateManager.InitialHints.ToString();
            TxtTimer.Text = "00:00";
            TxtNotesIcon.Foreground = BrCtrl;
            TxtNotesLabel.Text = "Примітки";
            TxtSolveIcon.Text = "▶";
            TxtSolveLabel.Text = "Розв\u02bcязати";

            var diff = DifficultyLabel.Tag is Difficulty d ? d : Difficulty.Easy;
            var (puzzle, solution, actualDiff) = SudokuGenerator.Generate(diff);
            _board.LoadFromArrays(puzzle, solution);

            DifficultyLabel.Text = actualDiff switch
            {
                ActualDifficulty.Easy => "Легкий (Naked Singles)",
                ActualDifficulty.Medium => "Середній (Hidden Singles)",
                ActualDifficulty.Hard => "Важкий (Trial & Error)",
                _ => "Легкий",
            };

            RefreshAll();
            _timer.Stop();
            _timer.Start();
        }

        // ═══════════════════════════════════════════════════════════
        //  Відображення клітинок (делегується ICellRenderer)
        // ═══════════════════════════════════════════════════════════

        private void RefreshAll()
        {
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    RefreshCell(r, c);
            RefreshHighlights();
            UpdateNumberPadState();
        }

        /// <summary>
        /// Оновлює відображення однієї клітинки.
        /// Принцип DIP: логіка рендерингу знаходиться у ICellRenderer,
        /// MainWindow лише координує виклик.
        /// </summary>
        private void RefreshCell(int row, int col)
        {
            var cell = _board[row, col];
            bool isConflicting = cell.Value != 0 && !_board.IsValidMove(row, col, cell.Value);

            _cellRenderer.RenderCell(
                cell,
                _cellBorders[row, col],
                _cellTexts[row, col],
                _noteGrids[row, col],
                _noteTexts[row, col],
                isConflicting);
        }

        private void RefreshHighlights()
        {
            if (!_visualHelp)
            {
                for (int r = 0; r < 9; r++)
                    for (int c = 0; c < 9; c++)
                        _cellBorders[r, c].Background = BrNormal;
                if (_selRow >= 0 && _selCol >= 0)
                    _cellBorders[_selRow, _selCol].Background = BrSelected;
                return;
            }

            int selVal = (_selRow >= 0 && _selCol >= 0)
                ? _board[_selRow, _selCol].Value : 0;

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                {
                    bool isSel = r == _selRow && c == _selCol;
                    bool sameRow = _selRow >= 0 && r == _selRow;
                    bool sameCol = _selCol >= 0 && c == _selCol;
                    bool sameBlock = _selRow >= 0
                                      && r / 3 == _selRow / 3 && c / 3 == _selCol / 3;
                    bool sameValue = selVal > 0
                                      && _board[r, c].Value == selVal && !isSel;

                    _cellBorders[r, c].Background =
                        isSel ? BrSelected :
                        sameValue ? BrSameVal :
                        (sameRow || sameCol || sameBlock) ? BrHighlight :
                        BrNormal;
                }
        }

        // ═══════════════════════════════════════════════════════════
        //  Ввід
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
            if (_board[_selRow, _selCol].IsFixed) return;
            if (_gameState.IsGameOver) return;

            // Режим нотаток: ToggleNote через Cell
            if (_notesMode)
            {
                if (_board[_selRow, _selCol].Value != 0) return;
                _board[_selRow, _selCol].ToggleNote(digit);
                RefreshCell(_selRow, _selCol);
                return;
            }

            _board.SetCell(_selRow, _selCol, digit);

            if (digit != 0)
            {
                _gameState.RegisterMove();
                if (!_board.IsValidMove(_selRow, _selCol, digit))
                {
                    bool gameOver = _gameState.RegisterError();
                    TxtErrors.Text = $"{_gameState.ErrorCount} / {GameStateManager.MaxErrors}";

                    if (gameOver)
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
            if (_board[_selRow, _selCol].IsFixed) return;
            _board.SetCell(_selRow, _selCol, 0);
            _board[_selRow, _selCol].ClearNotes();
            RefreshCell(_selRow, _selCol);
            RefreshHighlights();
            UpdateNumberPadState();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (PanelGame.Visibility != Visibility.Visible) return;

            if (e.Key >= Key.D1 && e.Key <= Key.D9)
            { InputDigit(e.Key - Key.D0); e.Handled = true; }
            else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
            { InputDigit(e.Key - Key.NumPad0); e.Handled = true; }
            else if (e.Key is Key.Delete or Key.Back or Key.D0 or Key.NumPad0)
            { ClearCurrentCell(); e.Handled = true; }
            else if (e.Key == Key.Up && _selRow > 0) { _selRow--; RefreshHighlights(); e.Handled = true; }
            else if (e.Key == Key.Down && _selRow < 8) { _selRow++; RefreshHighlights(); e.Handled = true; }
            else if (e.Key == Key.Left && _selCol > 0) { _selCol--; RefreshHighlights(); e.Handled = true; }
            else if (e.Key == Key.Right && _selCol < 8) { _selCol++; RefreshHighlights(); e.Handled = true; }
        }

        // ═══════════════════════════════════════════════════════════
        //  Перемога / Програш
        // ═══════════════════════════════════════════════════════════

        private void CheckWin()
        {
            if (!_board.IsComplete()) return;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (!_board.IsValidMove(r, c, _board[r, c].Value)) return;

            _timer.Stop();
            int m = _elapsedSec / 60, s = _elapsedSec % 60;

            var result = MessageBox.Show(
                $"Вітаємо! Судоку розв\u02bcязано!\n\n" +
                $"Час: {m:D2}:{s:D2}\n" +
                $"Заповнено клітинок:  {_gameState.PlayerMoves}\n" +
                $"Помилок:             {_gameState.ErrorCount}\n\n" +
                $"Зіграти ще раз?",
                "Перемога! 🎉",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                var dlg = new DifficultyDialog { Owner = this };
                if (dlg.ShowDialog() == true)
                    DifficultyLabel.Tag = dlg.SelectedDifficulty;
                StartNewGame();
            }
            else ShowPanel(PanelMenu);
        }

        private void ShowGameOver()
        {
            int m = _elapsedSec / 60, s = _elapsedSec % 60;
            var result = MessageBox.Show(
                $"Ви допустили {GameStateManager.MaxErrors} помилки. Гру завершено!\n\n" +
                $"Час: {m:D2}:{s:D2}\n" +
                $"Заповнено клітинок:  {_gameState.PlayerMoves}\n\n" +
                $"Спробувати ще раз?",
                "Гра закінчена",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) StartNewGame();
            else ShowPanel(PanelMenu);
        }

        // ═══════════════════════════════════════════════════════════
        //  Обробники кнопок
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
            BtnVisualHelp.Style = (Style)FindResource(
                _visualHelp ? "DarkPinkHoverBtn" : "PinkHoverBtn");
            RefreshHighlights();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            if (_board.Undo(out int row, out int col, out _))
            {
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
            if (_isRunning || !_gameState.HasHintsLeft) return;

            // Знаходимо клітинку для підказки
            if (_selRow < 0 || _selCol < 0
                || _board[_selRow, _selCol].IsFixed
                || _board[_selRow, _selCol].Value == _board[_selRow, _selCol].SolutionValue)
            {
                bool found = false;
                for (int r = 0; r < 9 && !found; r++)
                    for (int c = 0; c < 9 && !found; c++)
                        if (!_board[r, c].IsFixed
                            && _board[r, c].Value != _board[r, c].SolutionValue)
                        { _selRow = r; _selCol = c; found = true; }
                if (!found) return;
            }

            // SetCellHint позначає клітинку як IsHinted — рендерер покаже зеленим
            _board.SetCellHint(_selRow, _selCol, _board[_selRow, _selCol].SolutionValue);
            _gameState.UseHint();
            TxtHints.Text = _gameState.HintsLeft.ToString();

            RefreshCell(_selRow, _selCol);
            RefreshHighlights();
            UpdateNumberPadState();
            CheckWin();
        }

        private async void Solve_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                _manualCancel = true;
                _cts?.Cancel();
                return;
            }

            _manualCancel = false;

            var alg = RadioBacktracking.IsChecked == true
                ? SolverAlgorithm.Backtracking
                : SolverAlgorithm.ConstraintPropagation;

            ISudokuSolver solver = SudokuSolverFactory.Create(alg);

            if (RadioStep.IsChecked == true)
                await RunStepModeAsync(solver, alg);
            else
                await RunAutoModeAsync(solver, alg);
        }

        // ═══════════════════════════════════════════════════════════
        //  Режими солвера
        // ═══════════════════════════════════════════════════════════

        private async Task RunAutoModeAsync(ISudokuSolver solver, SolverAlgorithm alg)
        {
            _isRunning = true;
            TxtSolveIcon.Text = "■";
            TxtSolveLabel.Text = "Зупинити";

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var ct = _cts.Token;

            var grid = _board.CloneGrid();
            SolveStats stats = await Task.Run(() => solver.Solve(grid, ct));

            _isRunning = false;
            TxtSolveIcon.Text = "▶";
            TxtSolveLabel.Text = "Розв\u02bcязати";

            if (stats.TimedOut) { ShowTimeout(alg, stats); return; }

            bool hasSolution = true;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (grid[r, c] == 0) { hasSolution = false; break; }

            if (!hasSolution)
            {
                MessageBox.Show(
                    "Не вдалося знайти рішення для поточного стану дошки.\n" +
                    "Можливо, введені числа конфліктують між собою.",
                    "Рішення не знайдено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (!_board[r, c].IsFixed)
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

        private async Task RunStepModeAsync(ISudokuSolver solver, SolverAlgorithm alg)
        {
            // Знімаємо тільки фіксовані клітинки для солвера
            var startGrid = new int[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    startGrid[r, c] = _board[r, c].IsFixed ? _board[r, c].Value : 0;

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var computeCt = _cts.Token;

            var (steps, stats) = await Task.Run(() => solver.GetSteps(startGrid, computeCt));

            if (stats.TimedOut || steps == null)
            {
                _cts = null;
                ShowTimeout(alg, stats);
                return;
            }

            _board.Reset();
            RefreshAll();

            _isRunning = true;
            TxtSolveIcon.Text = "■";
            TxtSolveLabel.Text = "Стоп";

            // Анімація теж обмежена 2 хвилинами (відповідно до правил)
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var animCt = _cts.Token;

            try
            {
                foreach (var step in steps)
                {
                    animCt.ThrowIfCancellationRequested();

                    await Dispatcher.InvokeAsync(() =>
                    {
                        _board.SetCell(step.Row, step.Col, step.Value, addToHistory: false);
                        RefreshCell(step.Row, step.Col);
                        _cellBorders[step.Row, step.Col].Background =
                            step.IsBacktrack ? BrBacktrack : BrStep;
                    });

                    await Task.Delay(step.IsBacktrack ? 30 : 60, animCt);
                    await Dispatcher.InvokeAsync(RefreshHighlights);
                }

                _timer.Stop();
                string algName = alg == SolverAlgorithm.Backtracking ? "Backtracking" : "CSP";
                MessageBox.Show(
                    $"Алгоритм завершив розв\u02bcязання!\n\n" +
                    $"Алгоритм:         {algName}\n" +
                    $"Обчислення:       {stats.ElapsedMs} мс\n" +
                    $"Кроків вперед:    {stats.ForwardSteps}\n" +
                    $"Відкатів:         {stats.BacktrackSteps}\n" +
                    $"Всього ходів:     {stats.ForwardSteps + stats.BacktrackSteps}",
                    "Покроковий режим", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                if (!_manualCancel)
                {
                    // Відновлюємо початковий стан
                    for (int r = 0; r < 9; r++)
                        for (int c = 0; c < 9; c++)
                            _board.SetCell(r, c, startGrid[r, c], addToHistory: false);

                    // Миттєво застосовуємо всі кроки
                    foreach (var step in steps)
                        _board.SetCell(step.Row, step.Col, step.Value, addToHistory: false);

                    _timer.Stop();

                    MessageBox.Show(
                        $"Ліміт часу на анімацію вичерпано (2 хвилини)!\n\n" +
                        $"Алгоритм успішно знайшов рішення, але щоб не змушувати вас чекати, " +
                        $"ми пропустили залишок анімації та одразу вивели фінальний результат.\n\n" +
                        $"Обчислення:       {stats.ElapsedMs} мс\n" +
                        $"Кроків вперед:    {stats.ForwardSteps}\n" +
                        $"Відкатів:         {stats.BacktrackSteps}\n" +
                        $"Всього ходів:     {stats.ForwardSteps + stats.BacktrackSteps}",
                        "Анімацію пропущено", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            finally
            {
                _isRunning = false;
                TxtSolveIcon.Text = "▶";
                TxtSolveLabel.Text = "Розв\u02bcязати";
                _cts = null;
                RefreshAll();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Таймаут алгоритму
        // ═══════════════════════════════════════════════════════════

        private void ShowTimeout(SolverAlgorithm alg, SolveStats stats)
        {
            _timer.Stop();
            string algName = alg == SolverAlgorithm.Backtracking ? "Backtracking" : "CSP";
            string reason = alg == SolverAlgorithm.Backtracking
                ? "Backtracking у найгіршому випадку має складність O(9ⁿ). " +
                  "На дуже важких головоломках він може перебирати мільярди комбінацій. " +
                  "Спробуйте скинути поле або обрати CSP."
                : "CSP не зміг знайти рішення за допустимий час. " +
                  "Можливо, введені числа створюють суперечність. " +
                  "Перевірте коректність поля або почніть нову гру.";

            MessageBox.Show(
                $"Перевищено ліміт часу (2 хвилини)!\n\n" +
                $"Алгоритм:              {algName}\n" +
                $"Кроків до зупинки:     {stats.ForwardSteps}\n" +
                $"Відкатів до зупинки:   {stats.BacktrackSteps}\n\n" +
                $"Причина:\n{reason}",
                "Таймаут алгоритму",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Діалог вибору складності (для кнопки "Нова гра")
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Спливаюче вікно вибору складності.
    /// Окремий клас — принцип SRP: виклик нової гри відповідає за вибір,
    /// а MainWindow — за запуск.
    /// </summary>
    internal class DifficultyDialog : Window
    {
        public Difficulty SelectedDifficulty { get; private set; } = Difficulty.Easy;

        public DifficultyDialog()
        {
            Title = "Вибір складності";
            Width = 320; Height = 230;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF0F5"));
            FontFamily = new FontFamily("Segoe UI");

            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            panel.Children.Add(new TextBlock
            {
                Text = "Оберіть рівень складності:",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#880E4F")),
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Легкий   — лише одиночні кандидати\n" +
                             "Середній — потрібні приховані одиночки\n" +
                             "Важкий   — потрібен перебір",
                FontSize = 11.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                Margin = new Thickness(0, 0, 0, 14)
            });

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Рожева палітра для трьох кнопок — від світлого до темного
            (string Label, string Color, Difficulty Diff)[] items =
            {
                ("Легкий",   "#F06292", Difficulty.Easy),
                ("Середній", "#E91E63", Difficulty.Medium),
                ("Важкий",   "#AD1457", Difficulty.Hard),
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
                    Foreground = Brushes.White,
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
