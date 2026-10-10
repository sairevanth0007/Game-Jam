using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LumenWave
{
    /// <summary>
    /// Collectible sound fragment. Bobs and glows; touching it tells GameManager, which counts it and
    /// raises GameEvents.EchoCollected (audio plays the next note of the level's melody, FX sparkles, HUD counts).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Echo : MonoBehaviour
    {
        [SerializeField] SpriteRenderer crystal;
        [SerializeField] Light2D glow;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float bobSpeed = 2.4f;
        [SerializeField] float spinSpeed = 2f;

        Vector3 basePosition;
        float phase;
        float baseIntensity;
        bool collected;

        public void Setup(SpriteRenderer crystal, Light2D glow)
        {
            this.crystal = crystal;
            this.glow = glow;
            if (glow != null) baseIntensity = glow.intensity;
        }

        void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void Start()
        {
            basePosition = crystal != null ? crystal.transform.localPosition : Vector3.zero;
            phase = transform.position.x * 0.7f; // neighbours bob out of sync
            if (glow != null) baseIntensity = glow.intensity;
        }

        void Update()
        {
            phase += Time.deltaTime;
            if (crystal != null)
            {
                crystal.transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(phase * bobSpeed) * bobHeight);
                // Fake 3D spin: squash horizontally.
                float spin = Mathf.Abs(Mathf.Cos(phase * spinSpeed));
                crystal.transform.localScale = new Vector3(Mathf.Lerp(0.35f, 1f, spin), 1f, 1f);
            }
            if (glow != null)
                glow.intensity = baseIntensity * (0.8f + 0.2f * Mathf.Sin(phase * 3.1f));
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || !PlayerMovement.IsPlayer(other)) return;
            collected = true;
            if (GameManager.Instance != null) GameManager.Instance.CollectEcho(transform.position);
            Destroy(gameObject);
        }
    }
}
