namespace TrafficManager.U {
    using ColossalFramework;
    using System;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using UnityEngine;

    public static class CursorUtil {
        /// <summary>
        /// create cursor using default hot-spot of (5,0) and using a texture loaded from dll resource.
        /// </summary>
        /// <param name="resourceName">image to load</param>
        public static CursorInfo LoadCursorFromResource(string resourceName) =>
            LoadCursorFromResource(resourceName, new Vector2(5, 0));

        /// <summary>
        /// create cursor using a texture loaded from dll resource.
        /// Note: to destroy use GameObject.Destroy
        /// </summary>
        /// <param name="resourceName">image to load</param>
        /// <param name="hotSpot">coordinate in image that is the hot-spot of the cursor</param>
        public static CursorInfo LoadCursorFromResource(string resourceName, Vector2 hotSpot) {
            try {
                CursorInfo ret = ScriptableObject.CreateInstance<CursorInfo>();
                if (!resourceName.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) {
                    resourceName += ".png";
                }
                
                ret.m_texture = TextureResources.LoadDllResource(resourceName, new IntVector2(31, 31));
                ret.m_texture.wrapMode = TextureWrapMode.Clamp;
                ret.m_texture.Apply(false, false);
                ret.m_hotspot = hotSpot;
                return ret;
            } catch(Exception ex) {
                ex.LogException();
                return null;
            }
        }

        /// <summary>
        /// create cursor using the provided texture.
        /// </summary>
        /// <param name="texture">cursor texture</param>
        /// <param name="hotSpot">coordinate in image that is the hot-spot of the cursor</param>
        public static CursorInfo CreateCursor(Texture2D texture, Vector2 hotSpot) {
            CursorInfo ret = ScriptableObject.CreateInstance<CursorInfo>();
            ret.m_texture = texture;
            ret.m_hotspot = hotSpot;
            return ret;
        }

        /// <summary>
        /// destroy cursor that was created using <c>LoadCursorFromResource</c>.
        /// </summary>
        public static void DestroyCursorAndTexture(CursorInfo cursor) {
            GameObject.Destroy(cursor.m_texture);
            GameObject.Destroy(cursor);
        }
    }
}
