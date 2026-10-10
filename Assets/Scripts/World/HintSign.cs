using UnityEngine;

namespace LumenWave
{
    /// <summary>A hint stone: shows its text on the HUD while the player stands near it.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class HintSign : MonoBehaviour
    {
        [TextArea]
        [Tooltip("Shown at the bottom of the screen. Use {Jump}, {NoteLow} ... for current key names.")]
        [SerializeField] string text;
        [Tooltip("The glyph that brightens while the hint is showing.")]
        [SerializeField] SpriteRenderer glyph;
        [SerializeField] Color idleColor = new Color(0.55f, 0.5f, 0.75f, 0.7f);
        [SerializeField] Color activeColor = new Color(0.9f, 0.85f, 1f, 1f);

        bool showing;
        static HintSign current;

        public void Setup(string hintText, SpriteRenderer glyphRenderer)
        {
            text = hintText;
            glyph = glyphRenderer;
            if (glyph != null) glyph.color = idleColor;
        }

        void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!PlayerMovement.IsPlayer(other)) return;
            showing = true;
            current = this;
            GameEvents.RaiseHintChanged(BindingText.Format(text));
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!PlayerMovement.IsPlayer(other)) return;
            Hide();
        }

        void OnDisable() => Hide();

        void Hide()
        {
            if (!showing) return;
            showing = false;
            if (current == this)
            {
                current = null;
                GameEvents.RaiseHintChanged(null);
            }
        }

        void Update()
        {
            if (glyph == null) return;
            Color target = showing ? activeColor : idleColor;
            glyph.color = Color.Lerp(glyph.color, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
    }
}
