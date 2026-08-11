using System;
using System.Collections.Generic;
using MGSC;
using UnityEngine;

namespace QuasimorphLoadouts
{
    internal static class LoadoutIconResolver
    {
        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        internal static Sprite Resolve(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            if (Cache.TryGetValue(itemId, out Sprite cached))
            {
                return cached;
            }

            try
            {
                ItemFactory factory = SingletonMonoBehaviour<ItemFactory>.Instance;
                BasePickupItem item = factory?.CreateForInventory(itemId);
                ItemContentDescriptor descriptor = item?.View<ItemContentDescriptor>();
                Sprite sprite = item == null || descriptor == null
                    ? null
                    : factory.ResolveIcon(descriptor, item.InventoryWidthSize);
                Cache[itemId] = sprite;
                return sprite;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[QuasimorphLoadouts] Could not resolve icon for '{itemId}': {exception.Message}");
                Cache[itemId] = null;
                return null;
            }
        }

        internal static void Draw(Sprite sprite, Rect rect)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            Rect textureRect = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect coordinates = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
            float scale = Mathf.Min(rect.width / textureRect.width, rect.height / textureRect.height);
            Rect destination = new Rect(
                rect.center.x - textureRect.width * scale / 2f,
                rect.center.y - textureRect.height * scale / 2f,
                textureRect.width * scale,
                textureRect.height * scale);
            GUI.DrawTextureWithTexCoords(destination, texture, coordinates, alphaBlend: true);
        }
    }
}
