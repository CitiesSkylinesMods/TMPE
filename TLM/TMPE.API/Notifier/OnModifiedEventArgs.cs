namespace TrafficManager.API.Notifier {
    public class OnModifiedEventArgs {
        /// <summary>
        /// node/segment (citizen/vehicle in future) that has been modified.
        /// </summary>
        public InstanceID InstanceID;

        /// <summary>
        /// Reserved: the sender that caused the rule to be modified.
        /// </summary>
        public object Sender;

        /// <summary>
        /// Reserved: The rule that has been modified. object type is decided by sender.
        /// </summary>
        public object Data;
    }
}
