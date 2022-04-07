namespace TrafficManager.Manager.Impl.OverlayManagerData {
    using TrafficManager.API.Attributes;
    using TrafficManager.API.Traffic.Enums;
    using UnityEngine;

    public struct OverlayState {
        /// <summary>The position of camera, in screen space.</summary>
        internal Vector3 CameraPos => CameraInfo.m_position;

        internal RenderManager.CameraInfo CameraInfo; // ?

        /// <summary>The position of mouse, in screen space.</summary>
        internal Vector3 MousePos;

        /// <summary>Is <c>true</c> if primary mouse button pressed.</summary>
        internal bool Primary;

        /// <summary>Is <c>true</c> if secondary mouse button pressed.</summary>
        internal bool Secondary;

        /// <summary>Will be <c>true</c> if either mouse button pressed.</summary>
        [Cold]
        internal bool PrimaryOrSecondary => Primary || Secondary;

        /// <summary>Delta mouse scroll amount.</summary>
        // 0 = not scrolling; +ve = up, -ve = down
        [Cold]
        internal float WheelDelta => Input.mouseScrollDelta.y;

        [Cold]
        internal bool Shift;

        [Cold]
        internal bool Control;

        [Cold]
        internal bool Alt;

        internal InfoManager.InfoMode InfoMode => InfoManager.instance.CurrentMode;

        internal EntityType RefreshMapCache;
        internal Overlays RefreshOverlays;

        public Camera Camera => CameraInfo.m_camera;

        // z = 0.
        public Vector3 GetMousePos => Input.mousePosition;

        public float DistanceToMouse(Vector3 worldPos) =>
            (Input.mousePosition - Camera.WorldToScreenPoint(worldPos)).magnitude;

        // z is distance between worldPos and camera in world units; z will be >= 0 if on screen
        public Vector3 WorldToScreenPoint(Vector3 worldPos) =>
            CameraInfo.m_camera.WorldToScreenPoint(worldPos);

        // Based on Quistar's ECheckRenderDistance
        // https://github.com/Quistar-LAB/EManagersLib/blob/master/EMath.cs
        public bool IsInCameraRange(Vector3 worldPos, float maxDistance) {
            float distance = maxDistance * 0.45f;
            Vector3 campos = CameraInfo.m_position;
            Vector3 camforward = CameraInfo.m_forward;
            float x = worldPos.x - campos.x - camforward.x * distance;
            float y = worldPos.y - campos.y - camforward.y * distance;
            float z = worldPos.z - campos.z - camforward.z * distance;
            return (x * x + y * y + z * z) < maxDistance * maxDistance * 0.3025f;
        }
    }
}
