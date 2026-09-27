using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Cache
{
    /// <summary>
    /// Bounded sprite cache that frees what it drops. Past its capacity it releases the
    /// least recently used sprites — destroying the texture of those it created — but
    /// never one an Image is still showing, so eviction can't leave a white card behind.
    /// </summary>
    public sealed class SpriteLru
    {
        private struct Entry
        {
            public string Key;
            public Sprite Sprite;
            public bool Owned;
        }

        private readonly int _capacity;
        private readonly Dictionary<string, LinkedListNode<Entry>> _map = new();
        private readonly LinkedList<Entry> _order = new(); // front = most recent

        public SpriteLru(int capacity) => _capacity = Mathf.Max(1, capacity);

        public bool TryGet(string key, out Sprite sprite)
        {
            if (_map.TryGetValue(key, out var node) && node.Value.Sprite != null)
            {
                _order.Remove(node);
                _order.AddFirst(node);
                sprite = node.Value.Sprite;
                return true;
            }
            sprite = null;
            return false;
        }

        /// <summary>
        /// Stores <paramref name="sprite"/> and returns the sprite to use. When two loads
        /// of the same key race, the first one stored wins and the duplicate is freed.
        /// </summary>
        /// <param name="owned">True when the texture was created at runtime and may be
        /// destroyed; false for assets Unity manages (Resources).</param>
        public Sprite Put(string key, Sprite sprite, bool owned)
        {
            if (sprite == null) return null;

            if (_map.TryGetValue(key, out var existing) && existing.Value.Sprite != null)
            {
                if (existing.Value.Sprite != sprite && owned) Release(sprite);
                _order.Remove(existing);
                _order.AddFirst(existing);
                return existing.Value.Sprite;
            }

            if (existing != null) _order.Remove(existing);
            _map[key] = _order.AddFirst(new Entry { Key = key, Sprite = sprite, Owned = owned });
            Trim();
            return sprite;
        }

        private void Trim()
        {
            if (_order.Count <= _capacity) return;

            // Only paid when something has to go: one scan of the UI tells which
            // sprites are on screen (or parked on a hidden screen) right now.
            var inUse = new HashSet<Sprite>();
            foreach (var image in Object.FindObjectsByType<Image>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (image.sprite != null) inUse.Add(image.sprite);

            var node = _order.Last;
            while (node != null && _order.Count > _capacity)
            {
                var prev = node.Previous;
                var entry = node.Value;
                if (entry.Sprite == null || !inUse.Contains(entry.Sprite))
                {
                    _order.Remove(node);
                    _map.Remove(entry.Key);
                    if (entry.Owned) Release(entry.Sprite);
                }
                node = prev;
            }
        }

        private static void Release(Sprite sprite)
        {
            if (sprite == null) return;
            var tex = sprite.texture;
            Object.Destroy(sprite);
            if (tex != null) Object.Destroy(tex);
        }
    }
}
