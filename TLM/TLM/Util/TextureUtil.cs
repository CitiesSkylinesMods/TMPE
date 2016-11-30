using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Util {
	public static class TextureUtil {
		public static UITextureAtlas GenerateLinearAtlas(string name, Texture2D texture, int numSprites, string[] spriteNames) {
			UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
			atlas.padding = 0;
			atlas.name = name;

			var shader = Shader.Find("UI/Default UI Shader");
			if (shader != null)
				atlas.material = new Material(shader);
			atlas.material.mainTexture = texture;

			float spriteWidth = (float)texture.width / (float)numSprites;

			for (int i = 0; i < numSprites; ++i) {
				float x = (float)i / (float)numSprites;

				var sprite = new UITextureAtlas.SpriteInfo {
					name = spriteNames[i],
					region = new Rect(x, 0f, spriteWidth / (float)texture.width, 1f)					
				};

				var spriteTexture = new Texture2D((int)spriteWidth, texture.height);
				spriteTexture.SetPixels(texture.GetPixels((int)((float)texture.width * sprite.region.x), (int)((float)texture.height * sprite.region.y), (int)spriteWidth, texture.height));
				sprite.texture = spriteTexture;

				atlas.AddSprite(sprite);
			}

			return atlas;
		}
	}
}
