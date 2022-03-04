namespace TrafficManager.API {
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Notifier;
    using System.Linq;

    public static class Implementations {
        private static Type constantsType_;
        private static IManagerFactory managerFactory_;
        private static INotifier notifier_;

        public static IManagerFactory ManagerFactory => managerFactory_ ??= GetImplementation<IManagerFactory>();
        public static INotifier Notifier => notifier_ ??= GetImplementation<INotifier>();

        private static T GetImplementation<T>()
            where T : class {
            constantsType_ ??= Type.GetType("TrafficManger.Constants, TrafficManager", throwOnError: true);
            var field = constantsType_.GetFields().Single(item => typeof(T).IsAssignableFrom(item.FieldType));
            return field.GetValue(null) as T;
        }
    }
}
