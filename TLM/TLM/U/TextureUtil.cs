namespace TrafficManager.U {
    using System;
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.UI.Textures;
    using UnityEngine;
    using TrafficManager.Util;

    public static class TextureUtil {
        /// <summary>
        /// Initializes a new instance of the <see cref="UITextureAtlas"/> class from resource names list.
        /// NOTE: For UI sprites loading use <see cref="U.AtlasBuilder"/> instead, do not call this.
        /// </summary>
        /// <param name="atlasName">Name for the new atlas.</param>
        /// <param name="resourcePrefix">Prefix to resource directory.</param>
        /// <param name="spriteDefs">Names of textures and sizes to load.</param>
        /// <param name="atlasSizeHint">Texture with this side size is created.</param>
        public static UITextureAtlas CreateAtlas(string atlasName,
                                                 string resourcePrefix,
                                                 U.AtlasSpriteDef[] spriteDefs,
                                                 IntVector2 atlasSizeHint) {
            var loadedTextures = new List<Texture2D>(spriteDefs.Length);
            var loadedSpriteNames = new List<string>();

            // Load separate sprites and then pack it in a texture together
            foreach (U.AtlasSpriteDef spriteDef in spriteDefs) {
                // Allow spritedef resouce prefix to override prefix given to this func
                string prefix = string.IsNullOrEmpty(spriteDef.ResourcePrefix)
                    ? resourcePrefix
                    : spriteDef.ResourcePrefix;

                //--------------------------
                // Try loading the texture
                //--------------------------
                string resourceName = $"{prefix}.{spriteDef.Name}.png";
                Log._Debug($"TextureUtil: Loading {resourceName} for sprite={spriteDef.Name}");

                Texture2D tex = TextureResources.LoadDllResource(
                    resourceName: resourceName,
                    size: spriteDef.Size);

                if (tex != null) {
                    loadedTextures.Add(tex);
                    loadedSpriteNames.Add(spriteDef.Name); // only take those which are loaded
                }
                // No error reporting, it is done in LoadDllResource
            }

            Log._Debug($"TextureUtil: Atlas textures loaded, in {spriteDefs.Length}, success {loadedTextures.Count}");
            return PackTextures(atlasName, atlasSizeHint, loadedTextures, loadedSpriteNames);
        }

        private static UITextureAtlas PackTextures(string atlasName,
                                                   IntVector2 atlasSizeHint,
                                                   List<Texture2D> loadedTextures,
                                                   List<string> loadedSpriteNames) {
            Texture2D texture2D = new Texture2D(
                width: atlasSizeHint.x,
                height: atlasSizeHint.y,
                format: TextureFormat.ARGB32,
                mipmap: false);

            Rect[] regions = texture2D.PackTextures(
                textures: loadedTextures.ToArray(),
                padding: 2,
                maximumAtlasSize: Math.Max(atlasSizeHint.x, atlasSizeHint.y));

            // Now using loaded and packed textures, create the atlas with sprites
            UITextureAtlas newAtlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            var uiView = UIView.GetAView();
            Material material = UnityEngine.Object.Instantiate(uiView.defaultAtlas.material);

            material.mainTexture = texture2D;
            newAtlas.material = material;
            newAtlas.name = atlasName;

            for (int i = 0; i < loadedTextures.Count; i++) {
                var item = new UITextureAtlas.SpriteInfo {
                    name = loadedSpriteNames[i],
                    texture = loadedTextures[i],
                    region = regions[i],
                };
                newAtlas.AddSprite(item);
            }

            return newAtlas;
        }

        /// <summary>Creates new texture with changed alpha transparency for every pixel.</summary>
        /// <param name="tex">Copy from.</param>
        /// <param name="alpha">New alpha.</param>
        /// <returns>New texture.</returns>
        public static Texture2D AdjustAlpha(Texture2D tex, float alpha) {
            Color[] texColors = tex.GetPixels();
            Color[] retPixels = new Color[texColors.Length];

            for (int i = 0; i < texColors.Length; ++i) {
                retPixels[i] = new Color(
                    texColors[i].r,
                    texColors[i].g,
                    texColors[i].b,
                    texColors[i].a * alpha);
            }

            Texture2D ret = new Texture2D(tex.width, tex.height, TextureFormat.ARGB32, false);

            ret.SetPixels(retPixels);
            ret.Apply();

            return ret;
        }

        public static UITextureAtlas FindAtlas(string name) {
            UITextureAtlas[] atlases =
                Resources.FindObjectsOfTypeAll(typeof(UITextureAtlas)) as UITextureAtlas[];
            for (int i = 0; i < atlases.Length; i++) {
                if (atlases[i].name == name) {
                    return atlases[i];
                }
            }

            return UIView.GetAView().defaultAtlas;
        }
    } // end class
}