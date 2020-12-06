namespace TrafficManager.U {
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework.UI;
    using TrafficManager.Util;

    /// <summary>
    /// Populates a set of spritedefs as your UI form is populated with controls. Allows to use
    /// same atlas for all UI controls on a form.
    /// </summary>
    public class AtlasBuilder {
        private HashSet<U.AtlasSpriteDef> spriteDefs_ = new HashSet<AtlasSpriteDef>();

        public HashSet<AtlasSpriteDef> SpriteDefs => spriteDefs_;

        /// <summary>
        /// Add one more sprite to load.
        /// Use via <see cref="ButtonSkin.UpdateAtlasBuilder"/> where required sprites are added.
        /// </summary>
        /// <param name="spriteDef">Sprite size, name, filename, optional path etc.</param>
        public void Add(AtlasSpriteDef spriteDef) {
            this.spriteDefs_.Add(spriteDef);
        }

        /// <summary>Following the settings in the Skin fields, load sprites into an UI atlas.
        /// Longer list of atlas keys can be loaded into one atlas.</summary>
        /// <param name="atlasName">The name to append to "TMPE_U_***".</param>
        /// <param name="loadingPath">Path inside Resources. directory (dot separated).</param>
        /// <param name="atlasSizeHint">Square atlas of this size is created.</param>
        /// <returns>New UI atlas.</returns>
        public UITextureAtlas CreateAtlas(string atlasName,
                                          string loadingPath,
                                          IntVector2 atlasSizeHint) {
            string fullName = $"TMPE_U_{atlasName}";
            UITextureAtlas foundAtlas = TextureUtil.FindAtlas(fullName);

            // If is NOT the same as UI default, means the atlas is already loaded (return cached)
            if (!System.Object.ReferenceEquals(foundAtlas, UIView.GetAView().defaultAtlas)) {
                return foundAtlas;
            }

            return TextureUtil.CreateAtlas(
                atlasName: fullName,
                resourcePrefix: loadingPath,
                spriteDefs: spriteDefs_.ToArray(),
                atlasSizeHint: atlasSizeHint);
        }
    }
}