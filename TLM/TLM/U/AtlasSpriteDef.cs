namespace TrafficManager.U {
    using System;
    using TrafficManager.Util;

    /// <summary>
    /// Defines a sprite to be loaded into a Sprite Atlas (usually for buttons and other UI elements).
    /// </summary>
    // TODO: Can also store loading path here
    public class AtlasSpriteDef: IEquatable<AtlasSpriteDef> {
        /// <summary>Set this to nonempty string to load sprites from different resource path.</summary>
        public string ResourcePrefix;

        /// <summary>Sprite name in the atlas, and also PNG texture name.</summary>
        public string Name;

        /// <summary>Texture size assumed by the developer.</summary>
        public IntVector2 Size;

        public AtlasSpriteDef(string name, IntVector2 size, string prefix = "") {
            Name = name;
            Size = size;
            ResourcePrefix = prefix;
        }

        public bool Equals(AtlasSpriteDef other) {
            return other != null && this.Name == other.Name;
        }
    }
}