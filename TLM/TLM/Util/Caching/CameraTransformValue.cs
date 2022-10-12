namespace TrafficManager.Util.Caching {
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Stores transform of the camera, namely position, rotation.
    /// Fov and other parameters are not stored and not checked.
    /// Having two unequal transforms means that the camera has moved, or turned.
    /// </summary>
    public class CameraTransformValue {
        private Vector3 Position;
        private Quaternion Rot;

        public CameraTransformValue() {
            Position = Vector3.zero;
            Rot = Quaternion.identity;
        }

        public CameraTransformValue(Camera cam) {
            Transform t = cam.transform;
            Position = t.position;
            Rot = t.rotation;
        }

        public override bool Equals(object obj) {
            // assume this is only ever compared against another CameraTransformValue
            var other = (CameraTransformValue)obj;
            return FloatUtil.NearlyEqual(Position.x, other.Position.x)
                   && FloatUtil.NearlyEqual(Position.y, other.Position.y)
                   && FloatUtil.NearlyEqual(Position.z, other.Position.z)
                   && FloatUtil.NearlyEqual(Rot.x, other.Rot.x)
                   && FloatUtil.NearlyEqual(Rot.y, other.Rot.y)
                   && FloatUtil.NearlyEqual(Rot.z, other.Rot.z)
                   && FloatUtil.NearlyEqual(Rot.w, other.Rot.w);
        }
    }
}