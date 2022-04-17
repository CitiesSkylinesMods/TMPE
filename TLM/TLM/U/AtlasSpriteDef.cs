namespace TrafficManager.U {
    using System;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using UnityEngine;

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

        /// <summary>If defined, this will be used instead of loading a DLL resource.</summary>
        [CanBeNull]
        public Func<Texture2D> GetTexture;

        /// <summary>
        /// Initializes a new instance of the <see cref="AtlasSpriteDef"/> class.
        /// Set <see cref="GetTexture"/> to override DLL resource loading and provide an existing
        /// Texture2D from elsewhere, like pick a sign from a theme for example.
        /// </summary>
        /// <param name="name">Texture filename without PNG.</param>
        /// <param name="size">Size hint in pixels.</param>
        public AtlasSpriteDef(string name, IntVector2 size) {
            Name = name;
            Size = size;
        }

        public bool Equals(AtlasSpriteDef other) {
            return other != null && this.Name == other.Name;
        }

        public Texture2D LoadTexture2D(bool debugResourceLoading, string prefix) {
            if (this.GetTexture != null) {
                return this.GetTexture();
            }

            // Allow spritedef resouce prefix to override prefix given to this func
            string resourceName = this.Name.StartsWith("/")
                                      ? $"{this.Name.Substring(1)}.png"
                                      : $"{prefix}.{this.Name}.png";

            //--------------------------
            // Try loading the texture
            //--------------------------
            if (debugResourceLoading) {
                Log._Debug($"AtlasSpriteDef: Loading {resourceName} for sprite={this.Name}");
            }

            return TextureResources.LoadDllResource(
                resourceName: resourceName,
                size: this.Size);
        }
    }
}