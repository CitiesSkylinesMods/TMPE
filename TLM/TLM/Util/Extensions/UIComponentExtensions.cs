namespace TrafficManager.Util.Extensions {
    using System;
    using ColossalFramework.UI;

    internal static class UIComponentExtensions {
        internal static class Delegates {
            internal delegate void SuspendLayout(UIComponent component);
            internal delegate void ResumeLayout(UIComponent component);
        }

        private static Delegates.SuspendLayout suspendLayout_;
        private static Delegates.ResumeLayout resumeLayout_;

        static UIComponentExtensions() {
            suspendLayout_ = CreateDelegate<Delegates.SuspendLayout>();
            resumeLayout_ = CreateDelegate<Delegates.ResumeLayout>();
        }

        private static TDelegate CreateDelegate<TDelegate>()
            where TDelegate : Delegate =>
            TranspilerUtil.CreateDelegate<TDelegate>(type: typeof(UIComponent), name: typeof(TDelegate).Name, instance: true);

        public static void SuspendLayout(this UIComponent component) => suspendLayout_(component);
        public static void ResumeLayout(this UIComponent component) => resumeLayout_(component);
    }
}
