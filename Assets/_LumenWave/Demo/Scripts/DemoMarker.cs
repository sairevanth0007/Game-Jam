using UnityEngine;

namespace LumenWave.Demo
{
    public sealed class DemoMarker : MonoBehaviour
    {
        public DemoSession session;
        public bool goal;
        public Vector2 respawn;
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<ChordMotor>() == null) return;
            if (goal) session.Complete(); else session.Checkpoint(respawn);
        }
    }
}
