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
        /// <summary>
        /// Parameter used in <see cref="CreateAtlas"/> call.
        /// The name to append to "TMPE_U_***".
        /// </summary>
        private readonly string atlasName_;

        /// <summary>
        /// Parameter used in <see cref="CreateAtlas"/> call.
        /// Path inside Resources. directory (dot separated).
        /// </summary>
        private readonly string loadingPath_;

        /// <summary>
        /// Parameter used in <see cref="CreateAtlas"/> call.
        /// Square atlas of this size is created.
        /// </summary>
        private readonly IntVector2 sizeHint_;

        private HashSet<U.AtlasSpriteDef> spriteDefs_ = new HashSet<AtlasSpriteDef>();

        public AtlasBuilder(string atlasName, string loadingPath, IntVector2 sizeHint) {
            atlasName_ = atlasName;
            loadingPath_ = loadingPath;
            sizeHint_ = sizeHint;
        }
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
        /// <returns>New UI atlas.</returns>
        public UITextureAtlas CreateAtlas() {
            string fullName = $"TMPE_U_{this.atlasName_}";
            UITextureAtlas foundAtlas = TextureUtil.FindAtlas(fullName);

            // If is NOT the same as UI default, means the atlas is already loaded (return cached)
            if (!System.Object.ReferenceEquals(foundAtlas, UIView.GetAView().defaultAtlas)) {
                return foundAtlas;
            }

            return TextureUtil.CreateAtlas(
                atlasName: fullName,
                resourcePrefix: this.loadingPath_,
                spriteDefs: spriteDefs_.ToArray(),
                atlasSizeHint: this.sizeHint_);
        }
    }
}