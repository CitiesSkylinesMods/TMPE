namespace TrafficManager.U {
    using System;
    using TrafficManager.Util;

    /// <summary>
    /// Defines a sprite to be loaded into a Sprite Atlas (usually for buttons and other UI elements).
    /// </summary>
    // TODO: Can also store loading path here
    public class AtlasSpriteDef : IEquatable<AtlasSpriteDef> {
        /// <summary>
        /// Sprite name in the atlas, and also PNG texture name.
        /// If the prefix begins with / you can write absolute resource path instead, and the atlas
        /// loading path will be ignored. Example: "/MainMenu.Tool.RoundButton".
        /// </summary>
        public string Name;

        /// <summary>Texture size assumed by the developer.</summary>
        public IntVector2 Size;

        public AtlasSpriteDef(string name, IntVector2 size) {
            Name = name;
            Size = size;
        }

        public bool Equals(AtlasSpriteDef other) {
            return other != null && this.Name == other.Name;
        }
    }
}