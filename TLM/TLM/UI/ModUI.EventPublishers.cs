namespace TrafficManager.UI {
    using System;
    using TrafficManager.Util;

    public partial class ModUI {
        /// <summary>
        /// 1. In observing object inherit from (for example) IObserver{UIOpacityNotification} and
        ///    subscribe via creating a variable: IDisposable unsubscriber_;
        /// 2. initialize the variable: unsubscriber_ = ModUI.Instance.Events.UiScale.Subscribe(this);
        /// 3. in public override void OnDestroy() (for MonoBehaviours, and for LegacySubtools and TrafficManagerSubtools)
        ///    add: unsubscriber_.Dispose();
        /// </summary>
        public class EventPublishers {
            //--------------------------------------
            // UI Scale Notification
            //--------------------------------------

            /// <summary>Event fired when UI scale changes in the General Options tab.</summary>
            public struct UIScaleNotification { }

            public class UIScaleObservable : GenericObservable<UIScaleNotification> { }

            /// <summary>
            /// Subscribe to this to get notifications in your UI about UI scale changes (slider in
            /// General options tab).
            /// </summary>
            [NonSerialized]
            public UIScaleObservable UiScale = new();

            /// <summary>Call this to notify observing objects about UI scale change.</summary>
            public void UiScaleChanged() {
                this.UiScale.NotifyObservers(default);
            }

            //--------------------------------------
            // UI Language Notification
            //--------------------------------------

            /// <summary>Event fired when UI language is changed in options.</summary>
            public struct LanguageChangeNotification { }

            public class
                LanguageChangeObservable : GenericObservable<LanguageChangeNotification> { }

            [NonSerialized]
            public LanguageChangeObservable UiLanguage = new();

            /// <summary>Call this to notify observing objects about language change.</summary>
            public void LanguageChanged() {
                this.UiLanguage.NotifyObservers(default);
            }

            //--------------------------------------
            // UI Opacity Notification
            //--------------------------------------

            /// <summary>Event to be sent when UI transparency slider changes in the General Options tab.</summary>
            public struct UIOpacityNotification {
                public U.UOpacityValue Opacity;
            }

            public class UIOpacityObservable : GenericObservable<UIOpacityNotification> { }

            /// <summary>
            /// Subscribe to this to get notifications in your UI about UI transparency changes
            /// (slider in General options tab).
            /// </summary>
            [NonSerialized]
            public UIOpacityObservable UiOpacity = new();

            /// <summary>Call this to notify observing objects about UI opacity change.</summary>
            public void OpacityChanged(U.UOpacityValue opacity) {
                this.UiOpacity.NotifyObservers(new UIOpacityNotification { Opacity = opacity, });
            }

            //--------------------------------------
            // MPH/Kmph Display Change
            //--------------------------------------

            /// <summary>Event to be sent when MPH display option changes in the General Options tab.</summary>
            public struct DisplayMphNotification {
                public bool DisplayMph;
            }

            public class DisplayMphObservable : GenericObservable<DisplayMphNotification> { }

            /// <summary>
            /// Subscribe to this to get notifications in your UI about Display Mph option changes
            /// (checkbox in General options tab).
            /// </summary>
            [NonSerialized]
            public DisplayMphObservable DisplayMph = new();

            /// <summary>Call this to notify observing objects about Display Mph checkbox change.</summary>
            public void DisplayMphChanged(bool newDisplayMph) {
                this.DisplayMph.NotifyObservers(new DisplayMphNotification { DisplayMph = newDisplayMph, });
            }
        } // class ModUI.EventPublishers
    } // class ModUI
}