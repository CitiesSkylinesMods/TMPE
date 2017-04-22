using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Util {
	public static class TextureUtil {
		public static UITextureAtlas GenerateLinearAtlas(string name, Texture2D texture, int numSprites, string[] spriteNames) {
			return Generate2DAtlas(name, texture, numSprites, 1, spriteNames);
		}

		public static UITextureAtlas Generate2DAtlas(string name, Texture2D texture, int numX, int numY, string[] spriteNames) {
			if (spriteNames.Length != numX * numY) {
				throw new ArgumentException($"Number of sprite name does not match dimensions (expected {numX} x {numY}, was {spriteNames.Length})");
			}

			UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
			atlas.padding = 0;
			atlas.name = name;

			var shader = Shader.Find("UI/Default UI Shader");
			if (shader != null)
				atlas.material = new Material(shader);
			atlas.material.mainTexture = texture;

			int spriteWidth = Mathf.RoundToInt((float)texture.width / (float)numX);
			int spriteHeight = Mathf.RoundToInt((float)texture.height / (float)numY);

			int k = 0;
			for (int i = 0; i < numX; ++i) {
				float x = (float)i / (float)numX;
				for (int j = 0; j < numY; ++j) {
					float y = (float)j / (float)numY;

					var sprite = new UITextureAtlas.SpriteInfo {
						name = spriteNames[k],
						region = new Rect(x, y, (float)spriteWidth / (float)texture.width, (float)spriteHeight / (float)texture.height)
					};

					var spriteTexture = new Texture2D(spriteWidth, spriteHeight);
					spriteTexture.SetPixels(texture.GetPixels((int)((float)texture.width * sprite.region.x), (int)((float)texture.height * sprite.region.y), spriteWidth, spriteHeight));
					sprite.texture = spriteTexture;

					atlas.AddSprite(sprite);

					++k;
				}
			}

			return atlas;
		}
	}
}
