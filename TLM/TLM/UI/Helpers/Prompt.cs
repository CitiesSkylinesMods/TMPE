namespace TrafficManager.UI.Helpers {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using TrafficManager.Lifecycle;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Use this class to display small but annoying dialog prompts to the user.
    ///
    /// TODO: At some point add more panels, such as:
    /// * ConfirmPanel
    /// * ExitConfirmPanel
    /// * MessageBoxPanel
    /// * TutorialPanel
    /// * TutorialAdvisorPanel
    /// </summary>
    public class Prompt {

        /// <summary>
        /// Display an info prompt in the centre of the screen.
        /// </summary>
        ///
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog body text.</param>
        public static void Info(string title, string message) {
            MessageBoxPanel(title, message);
        }

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
        /// Display a formatted warning prompt in the center of the screen.
        /// </summary>
        /// 
        /// <param name="title">Dialog title.</param>
        /// <param name="messageFormat">Dialog body text format.</param>
        /// <param name="args">Values to put in the <paramref name="messageFormat"/>.</param>
        public static void WarningFormat(string title, string messageFormat, params object[] args) {
            ExceptionPanel(title, string.Format(messageFormat, args), false);
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
        /// Display an formatted error prompt in the center of the screen.
        /// </summary>
        /// 
        /// <param name="title">Dialog title.</param>
        /// <param name="messageFormat">Dialog body text format.</param>
        /// <param name="args">Values to put in the <paramref name="messageFormat"/>.</param>
        public static void ErrorFormat(string title, string messageFormat, params object[] args) {
            ExceptionPanel(title, string.Format(messageFormat, args), true);
        }

        /// <summary>
        /// Display an exception message in the center of the screen, optionally
        /// styled as an error.
        /// </summary>
        /// 
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog body text.</param>
        /// <param name="isError">If <c>true</c>, the dialog is styled as an error.</param>
        private static void ExceptionPanel(string title, string message, bool isError) {
            Action prompt = () => {
                UIView.library
                    .ShowModal<ExceptionPanel>("ExceptionPanel")
                    .SetMessage(title, message, isError);
            };

            try {
                if (TMPELifecycle.InGameOrEditor()) {
                    Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(prompt);
                } else {
                    prompt();
                }
            } catch (Exception e) {
                Log.ErrorFormat(
                    "Error displaying a Prompt:\n{0}",
                    e.ToString());
            }
        }

        /// <summary>
        /// Display an info message dialog in the center of the screen
        /// </summary>
        ///
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog body text.</param>
        private static void MessageBoxPanel(string title, string message) {
            Action prompt = () => {
                MessageBoxPanel messageBoxPanel = UIView.library
                                                        .ShowModal<MessageBoxPanel>("MessageBoxPanel");
                messageBoxPanel.Find<UILabel>("Message").text = message;
                messageBoxPanel.Find<UILabel>("Caption").text = title;
                UIButton uiButton = messageBoxPanel.Find<UIButton>("Close");
                if (!uiButton)
                    return;
                uiButton.isVisible = false;
            };

            try {
                if (TMPELifecycle.InGameOrEditor()) {
                    Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(prompt);
                } else {
                    prompt();
                }
            } catch (Exception e) {
                Log.ErrorFormat(
                    "Error displaying a Prompt:\n{0}",
                    e.ToString());
            }
        }
    }
}
