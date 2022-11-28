namespace TrafficManager.State.Helpers {
    using ICities;

    public class ActionButton : OptionButtonBase {

        public OnButtonClicked Handler {
            set {
                OnClicked -= value;
                OnClicked += value;
            }
        }
    }
}
