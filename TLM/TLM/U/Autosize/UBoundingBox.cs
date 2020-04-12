namespace TrafficManager.U.Autosize {
    using ColossalFramework.UI;
    using UnityEngine;

    /// <summary>A simple Axis Aligned Bounding Box (aabb) similar to Unity's Bounds.</summary>
    public struct UBoundingBox {
        public Vector2 A;
        public Vector2 B;

        public UBoundingBox(Vector2 a, Vector2 b) {
            this.A = a;
            this.B = b;
        }

        public UBoundingBox(UIComponent control) {
            this.A = control.relativePosition;
            this.B = this.A + control.size;
        }

        /// <summary>Gets box size.</summary>
        public Vector2 Size => this.B - this.A;

        /// <summary>Grow the bounding box to include the new box.</summary>
        /// <param name="box">The new box.</param>
        public void ExpandToFit(UBoundingBox box) {
            A.x = Mathf.Min(A.x, box.A.x);
            A.y = Mathf.Min(A.y, box.A.y);

            B.x = Mathf.Max(B.x, box.B.x);
            B.y = Mathf.Max(B.y, box.B.y);
        }

        public override string ToString() {
            return $"UBBox{{a={A}, b={B}}}";
        }

        public float Width => this.B.x - this.A.x;
        public float Height => this.B.y - this.A.y;
    }
}