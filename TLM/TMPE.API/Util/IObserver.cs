namespace TrafficManager.API.Util {
    public interface IObserver<T> {
        void OnUpdate(T subject);
    }
}