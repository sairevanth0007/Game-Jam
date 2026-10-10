using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// A raycast light beam on the X/Y plane (contract stub - the light worker owns the implementation).
    /// Public fields are the stable API used by level entities and enemies (e.g. the Sentinel sweeps one).
    /// </summary>
    public partial class LightBeam : MonoBehaviour
    {
        public LightColor color = LightColor.White;
        /// <summary>Direction in the X/Y plane (normalised by the beam).</summary>
        public Vector2 direction = Vector2.down;
        public float range = 16f;
        public float intensity = 1f;
        public float width = 0.6f;
        /// <summary>Yellow-style: only lit during the BeatClock hot window.</summary>
        public bool beatGated;
        public bool on = true;
    }
}
