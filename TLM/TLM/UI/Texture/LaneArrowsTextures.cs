namespace TrafficManager.UI.Texture {
    using CSUtil.Commons;
    using UnityEngine;

    /// <summary>
    /// Groups resources for Lane Arrows Tool, 32x32
    /// Row 0 (64px) contains: red X, green ↑, →, ↑→, ←, ←↑, ←→, ←↑→
    /// Row 1 (32px) contains: Blue ←, ↑, →, disabled ←, ↑, →
    /// Row 2 (0px) contains: black ←, ↑, →
    /// </summary>
    internal struct LaneArrowsTextures {
        private const int ATLAS_H = 96;
        private const float SPRITE_H = 32f;
        private const float SPRITE_W = 32f;

        /// <summary>
        /// Atlas with icons 16x16 for small UI elements
        /// </summary>
        private static readonly Texture2D Atlas32;

        static LaneArrowsTextures() {
            Atlas32 = TextureResources.LoadDllResource("LaneArrows.Atlas_Lane_Arrows.png", 256, ATLAS_H);
        }

        /// <summary>
        /// Returns sprite, where rect has Y inverted, because Unity has Y axis going up...
        /// </summary>
        /// <param name="x">Horizontal sprite number</param>
        /// <param name="y">Vertical sprite number, 0 = top row</param>
        /// <returns>Rect</returns>
        public static Sprite GetSprite(int x, int y) {
            var rc = new Rect(x * 32f, ATLAS_H - 32f - (y * 32f),
                              SPRITE_H, SPRITE_W);
            return Sprite.Create(Atlas32, rc, Vector2.zero);
        }

        /// <summary>
        /// The first row of the atlas contains arrow signs, where Left, Forward and Right form bit-combinations
        /// </summary>
        /// <param name="flags">Actual lane flags to display</param>
        /// <returns>A sprite</returns>
        public static Sprite GetLaneControlSprite(NetLane.Flags flags) {
            var forward = (flags & NetLane.Flags.Forward) != 0 ? 1 : 0;
            var right = (flags & NetLane.Flags.Right) != 0 ? 2 : 0;
            var left = (flags & NetLane.Flags.Left) != 0 ? 4 : 0;
            var spriteIndex = forward | left | right;
            return GetSprite(spriteIndex, 0);
        }

        /// <summary>
        /// For lane direction and possibly disabled lane, return a sprite
        /// </summary>
        /// <param name="dir">Direction</param>
        /// <param name="disabled">Whether the sprite should be gray and crossed out</param>
        /// <returns>The sprite</returns>
        public static Sprite GetLaneArrowSprite(ArrowDirection dir, bool on, bool disabled) {
            var x = 0;
            var y = 0; // 0,0 is red x default fallback sprite

            switch (dir) {
                case ArrowDirection.Left:
                    x = 1;
                    y = 1;
                    break;
                case ArrowDirection.Forward:
                    x = 0;
                    y = 1;
                    break;
                case ArrowDirection.Right:
                    x = 2;
                    y = 1;
                    break;
                case ArrowDirection.None:
                case ArrowDirection.Turn:
                    break;
            }

            if (!on) {
                // off sprites are on row 2 (64px)
                y++;
            }

            if (disabled) {
                x += 3;
                y = 1;
            }

            return GetSprite(x, y);
        }
    }
}