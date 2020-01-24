namespace TrafficManager.Util {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.State.ConfigData;
    using UnityEngine;

    public static class TextureUtil {
        public static UITextureAtlas GenerateLinearAtlas(string name,
                                                         Texture2D texture,
                                                         int numSprites,
                                                         string[] spriteNames) {
            return Generate2DAtlas(name, texture, numSprites, 1, spriteNames);
        }

        public static UITextureAtlas Generate2DAtlas(string name,
                                                     Texture2D texture,
                                                     int numX,
                                                     int numY,
                                                     string[] spriteNames) {
            if (spriteNames.Length != numX * numY) {
                throw new ArgumentException(
                    "Number of sprite name does not match dimensions " +
                    $"(expected {numX} x {numY}, was {spriteNames.Length})");
            }

            Log._DebugIf(
                DebugSwitch.ResourceLoading.Get(),
                ()=>$"Loading atlas for {name} count:{numX}x{numY} " +
                $"texture:{texture.name} size:{texture.width}x{texture.height}");

            UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            atlas.padding = 0;
            atlas.name = name;

            Shader shader = Shader.Find("UI/Default UI Shader");

            if (shader != null) {
                atlas.material = new Material(shader);
            }

            atlas.material.mainTexture = texture;

            int spriteWidth = Mathf.RoundToInt(texture.width / (float)numX);
            int spriteHeight = Mathf.RoundToInt(texture.height / (float)numY);
            int k = 0;

            for (int i = 0; i < numX; ++i) {
                float x = i / (float)numX;
                for (int j = 0; j < numY; ++j) {
                    float y = j / (float)numY;

                    var sprite = new UITextureAtlas.SpriteInfo {
                        name = spriteNames[k],
                        region = new Rect(
                            x,
                            y,
                            spriteWidth / (float)texture.width,
                            spriteHeight / (float)texture.height)
                    };

                    var spriteTexture = new Texture2D(spriteWidth, spriteHeight);
                    spriteTexture.SetPixels(
                        texture.GetPixels(
                            (int)(texture.width * sprite.region.x),
                            (int)(texture.height * sprite.region.y),
                            spriteWidth,
                            spriteHeight));
                    sprite.texture = spriteTexture;

                    atlas.AddSprite(sprite);

                    ++k;
                }
            }

            return atlas;
        }
    } // end class
}