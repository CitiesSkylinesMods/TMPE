namespace TrafficManager.UI.MainMenu {
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Controls how buttons are placed on the main menu panel.
    /// Returned from menu building function and describes resulting button placement.
    /// </summary>
    public class MainMenuLayout {
        /// <summary>How many buttons were enabled and placed.</summary>
        private int count_;

        // /// <summary>
        // /// What's max column used (for window width). This equals to max count of buttons
        // /// per row reached while placing the buttons.
        // /// </summary>
        // public int MaxCols;

        public MainMenuLayout() {
            count_ = 0;
            // MaxCols = 0;
        }

        public int Rows = 1;

        /// <summary>For a list of Buttons, count those which are visible.</summary>
        /// <param name="buttons">The list of <see cref="U.Button.UButton"/>.</param>
        public void CountEnabledButtons(IEnumerable<MenuButtonDef> buttonDefs) {
            // Store the count buttons which are enabled
            count_ = 0;
            foreach (MenuButtonDef bDef in buttonDefs) {
                if (bDef.IsEnabledFunc()) {
                    count_++;
                }
            }
        }

        /// <summary>
        /// Row breaking strategy for main menu tool button layout.
        /// If total buttons count is 4 or less: use one row, never break.
        /// If count is more than 4: try form 2 rows but first row will be least 4 up to 6 buttons.
        /// </summary>
        /// <param name="placedInARow">Current row build progress.</param>
        /// <param name="maxRowLength">Max column where the row should be broken no matter what.</param>
        /// <returns>Whether to break the row right now.</returns>
        public bool IsRowBreak(int placedInARow, int minRowLength, int maxRowLength) {
            if (count_ <= minRowLength) {
                return false; // do not break if less than 4 buttons
            }

            // Breakpoint will be half of buttons, no less than 4, and no more than MaxRowLength
            int breakPoint = Math.Min(maxRowLength, Math.Max(minRowLength, (count_ + 1) / 2));
            return placedInARow >= breakPoint;
        }
    }
}