namespace TrafficManager.UI.Helpers {
    using ColossalFramework;
    using ColossalFramework.UI;

    /// <summary>
    /// Use this class to display small dialog prompts to the user.
    ///
    /// Currently this is only tested for use in `LoadingExtension.OnLevelLoaded()`.
    /// </summary>
    public class Prompt {

        /// <summary>
        /// Display a warning prompt in the centre of the screen.
        /// </summary>
        /// 
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog body text.</param>
        public static void Warning(string title, string message) {
            ExceptionPanel(title, message, false);
        }

        /// <summary>
        /// Display an error prompt in the centre of the screen.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog body text.</param>
        public static void Error(string title, string message) {
            ExceptionPanel(title, message, true);
        }

        /// <summary>
        /// Display an exception message in the center of the screen, optionally
        /// styled as an error.
        /// </summary>
        /// 
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog body text.</param>
        /// <param name="isError">If <c>true</c>, the dialog is styled as an error.</param>
        internal static void ExceptionPanel(string title, string message, bool isError) {
            // todo: make sure it works everywhere:
            // * from main menu
            // * while a city is loading
            // * while in a city (in-game)
            // * while a city is unloading.

            Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(
                () => {
                    UIView.library
                        .ShowModal<ExceptionPanel>("ExceptionPanel")
                        .SetMessage(title, message, isError);
                });
        }
    }
}
