namespace TrafficManager.U.Autosize {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UnityEngine;

    /// <summary>Defines sizing and spacing for the control.</summary>
    public class USizePosition {
        public UValue Left = new UValue(USizeRule.FixedSize, 0f);
        public UValue Top = new UValue(USizeRule.FixedSize, 0f);

        /// <summary>How the width will be calculated, using the <see cref="WidthValue"/>.</summary>
        public UValue Width = new UValue(USizeRule.FixedSize, 16f);

        /// <summary>How the height will be calculated, using the <see cref="HeightValue"/>.</summary>
        public UValue Height = new UValue(USizeRule.FixedSize, 16f);

        // public static Vector2 GetReferenceSize(float pixelsAt1080p) {
        //     return new Vector2(pixelsAt1080p / 1920f, pixelsAt1080p / 1080f);
        // }

        /// <summary>
        /// Descends recursively into component children to calculate their sizes and positions,
        /// and from that derive our own size and position.
        /// </summary>
        /// <param name="self">The current component.</param>
        /// <returns>The calculated box (also size is updated).</returns>
        public UBoundingBox UpdateControl(UIComponent self) {
            // Create bounding box and update it with all children bounding boxes
            UBoundingBox allChildrenBox = default;

            for (int i = 0; i < self.transform.childCount - 1; i++) {
                Transform child = self.transform.GetChild(i);

                UIComponent uiComponent = child.gameObject.GetComponent<UIComponent>();
                if (uiComponent is USizePositionInterface asSizePos) {
                    // If the child gameobject is an UIComponent, and also is a USizePositionInterface
                    UBoundingBox childBox = asSizePos.SizePosition.UpdateControl(uiComponent);
                    allChildrenBox.ExpandToFit(childBox);
                }
            }

            self.position = CalculateControlPosition(self, self.position);
            self.size = CalculateControlSize(self, self.size, allChildrenBox.Size);
            Log._Debug($"USizePos: {self.name} pos={self.position} size={self.size}");
            return allChildrenBox;
        }

        private Vector2 CalculateControlSize(UIComponent self,
                                             Vector2 size,
                                             Vector2 allChildrenBox) {
            switch (this.Width.Rule) {
                case USizeRule.Ignore:
                    break;
                case USizeRule.FitChildrenWidth:
                    size.x = allChildrenBox.x;
                    break;
                default:
                    size.x = this.Width.Calculate(self);
                    break;
            }
            switch (this.Height.Rule) {
                case USizeRule.Ignore:
                    break;
                case USizeRule.FitChildrenHeight:
                    size.y = allChildrenBox.y;
                    break;
                default:
                    size.y = this.Height.Calculate(self);
                    break;
            }

            return size;
        }

        private Vector3 CalculateControlPosition(UIComponent self, Vector3 pos) {
            switch (this.Left.Rule) {
                case USizeRule.Ignore:
                    break;
                default:
                    pos.x = this.Left.Calculate(self);
                    break;
            }
            switch (this.Top.Rule) {
                case USizeRule.Ignore:
                    break;
                default:
                    pos.y = this.Top.Calculate(self);
                    break;
            }

            return pos;
        }
    }
}