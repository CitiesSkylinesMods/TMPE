using System;

namespace TrafficManager.State.Keybinds {
    /// <summary>
    /// This attribute is used on key bindings and tells us where this key is used,
    /// to allow using the same key in multiple occasions we need to know the category.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class TmpeRebindableKey: Attribute {
        public string Category;
        public TmpeRebindableKey(string cat) {
            Category = cat;
        }
    }
}