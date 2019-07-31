namespace TrafficManager.API.Util {
    public interface IVisitor<Target> {
        bool Visit(Target target);
    }
}