using System;

namespace SudokuApp.Game
{
    /// <summary>
    /// Керує станом ігрової сесії: помилки, підказки, ходи гравця.
    /// Принцип SRP: виділено з MainWindow, щоб вікно не відповідало
    /// за бізнес-логіку підрахунку помилок і підказок.
    /// Принцип OCP: нові правила (напр., бонусні підказки) можна додати
    /// без зміни MainWindow.
    /// Сповіщення через події дотримується принципу слабкого зв'язування.
    /// </summary>
    public class GameStateManager
    {
        // ── Константи ─────────────────────────────────────────────────────
        public const int MaxErrors = 3;
        public const int InitialHints = 3;

        // ── Властивості ───────────────────────────────────────────────────

        /// <summary>Кількість зроблених помилок (0..MaxErrors).</summary>
        public int ErrorCount { get; private set; }

        /// <summary>Кількість доступних підказок.</summary>
        public int HintsLeft { get; private set; }

        /// <summary>Кількість ходів, зроблених гравцем вручну.</summary>
        public int PlayerMoves { get; private set; }

        /// <summary>True, якщо гравець вичерпав усі спроби.</summary>
        public bool IsGameOver => ErrorCount >= MaxErrors;

        /// <summary>True, якщо є ще доступні підказки.</summary>
        public bool HasHintsLeft => HintsLeft > 0;

        // ── Події ─────────────────────────────────────────────────────────

        /// <summary>Виникає, коли досягнуто MaxErrors помилок.</summary>
        public event EventHandler? GameOver;

        /// <summary>Виникає при кожній новій помилці. Аргумент — нова кількість.</summary>
        public event EventHandler<int>? ErrorCountChanged;

        /// <summary>Виникає при використанні підказки. Аргумент — нова кількість, що залишилась.</summary>
        public event EventHandler<int>? HintsLeftChanged;

        // ── Ініціалізація ─────────────────────────────────────────────────

        public GameStateManager() => Reset();

        /// <summary>Скидає стан для нової гри.</summary>
        public void Reset()
        {
            ErrorCount = 0;
            HintsLeft = InitialHints;
            PlayerMoves = 0;
        }

        // ── Операції ─────────────────────────────────────────────────────

        /// <summary>
        /// Реєструє помилку гравця.
        /// </summary>
        /// <returns>True, якщо після цього гра закінчена (IsGameOver).</returns>
        public bool RegisterError()
        {
            if (IsGameOver) return true;
            ErrorCount++;
            ErrorCountChanged?.Invoke(this, ErrorCount);
            if (IsGameOver)
                GameOver?.Invoke(this, EventArgs.Empty);
            return IsGameOver;
        }

        /// <summary>
        /// Намагається використати підказку.
        /// </summary>
        /// <returns>True, якщо підказка була доступна і витрачена.</returns>
        public bool UseHint()
        {
            if (!HasHintsLeft) return false;
            HintsLeft--;
            HintsLeftChanged?.Invoke(this, HintsLeft);
            return true;
        }

        /// <summary>Реєструє хід гравця (лічильник).</summary>
        public void RegisterMove() => PlayerMoves++;
    }
}