namespace TrafficManager.U.Button {
    /// <summary>
    /// Defines tool types for TM:PE. Modes are exclusive, one can be active at a time.
    /// </summary>
    public class ButtonFunction {
        public string Name;
        public bool Enabled;

        public ButtonFunction(string name)
            : this(name, true) { }

        public ButtonFunction(string name, bool enabled) {
            Name = name;
            Enabled = enabled;
        }
    }
}