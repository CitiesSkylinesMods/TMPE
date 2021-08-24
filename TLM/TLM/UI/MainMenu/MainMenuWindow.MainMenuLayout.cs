namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;

    public partial class MainMenuWindow {
        /// <summary>
        /// Controls how buttons are placed on the main menu panel.
        /// Returned from menu building function and describes resulting button placement.
        /// </summary>
        public class MainMenuLayout {
            /// <summary>How many buttons were enabled and placed.</summary>
            private int count_;

            public MainMenuLayout() {
                count_ = 0;
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
            /// If total buttons count is 3 or less: use one row, never break.
            /// If count is more than 3: try split into 2 rows.
            /// </summary>
            /// <param name="placedInARow">Current row build progress.</param>
            /// <param name="minRowLength">How many buttons can be placed in a row without needing
            /// a break (minimum).</param>
            public bool IsRowBreak(int placedInARow, int minRowLength) {
                if (count_ <= minRowLength) {
                    return false; // do not break if less than 4 buttons
                }

                // Breakpoint will be half of buttons, no less than 4, and no more than MaxRowLength
                int breakPoint = (count_ + 1) / 2;
                return placedInARow >= breakPoint;
            }
        }
    }
}