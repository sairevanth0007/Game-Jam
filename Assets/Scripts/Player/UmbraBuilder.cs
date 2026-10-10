using UnityEngine;

namespace LumenWave
{
    /// <summary>Builds Umbra (the player) from code: physics body, collider, movement and visuals.</summary>
    public static class UmbraBuilder
    {
        public static readonly Color GlowColor = new Color(0.62f, 0.4f, 1f);

        /// <summary>Creates the player with its feet at the given position.</summary>
        public static PlayerMovement Create(Vector2 feetPosition)
        {
            var root = new GameObject("Umbra");
            root.transform.position = feetPosition;

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;

            var collider = root.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.68f, 0.74f);
            collider.offset = new Vector2(0f, 0.37f);

            var movement = root.AddComponent<PlayerMovement>();

            var squash = new GameObject("Visual").transform;
            squash.SetParent(root.transform, false);

            SpriteRenderer bodyRenderer = RuntimeArt.CreateSprite("Body", squash, RuntimeArt.UmbraBody, Color.white, RuntimeArt.SortPlayer, true);
            SpriteRenderer eyesRenderer = RuntimeArt.CreateSprite("Eyes", squash, RuntimeArt.UmbraEyes, Color.white, RuntimeArt.SortPlayer + 1, true, new Vector3(0f, 0.4f, 0f));

            var glow = RuntimeArt.CreatePointLight("Glow", root.transform, new Vector3(0f, 0.4f, 0f), GlowColor, 0.6f, 3.4f, 0.2f, 0.6f);

            var visual = root.AddComponent<PlayerVisual>();
            visual.Setup(movement, squash, bodyRenderer, eyesRenderer.transform, glow);
            return movement;
        }
    }
}
