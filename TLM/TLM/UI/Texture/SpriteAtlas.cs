namespace TrafficManager.UI.Texture {
    using UnityEngine;

    /// <summary>
    /// Loads a texture with sprites in a regular rectangular grid.
    /// Allows accessing sprites per row and column.
    /// </summary>
    public class SpriteAtlas {
        private readonly int atlasH_;
        private readonly float spriteH_;
        private readonly float spriteW_;

        /// <summary>
        /// Atlas with icons 16x16 for small UI elements
        /// </summary>
        private readonly Texture2D atlas_;

        public SpriteAtlas(int atlasW, int atlasH, float spriteW, float spriteH, string resource) {
            atlasH_ = atlasH;
            spriteW_ = spriteW;
            spriteH_ = spriteH;
            atlas_ = TextureResources.LoadDllResource(resource, atlasW, atlasH_);
        }

        /// <summary>
        /// Returns sprite, where rect has Y inverted, because Unity has Y axis going up...
        /// </summary>
        /// <param name="x">Horizontal sprite number</param>
        /// <param name="y">Vertical sprite number, 0 = top row</param>
        /// <returns>Rect</returns>
        public Sprite GetSprite(int x, int y) {
            var rc = new Rect(x * spriteW_, atlasH_ - spriteH_ - (y * spriteH_),
                              spriteH_, spriteW_);
            return Sprite.Create(atlas_, rc, Vector2.zero);
        }
    }
}