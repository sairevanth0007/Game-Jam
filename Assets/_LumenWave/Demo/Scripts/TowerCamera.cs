using UnityEngine;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(100)]
    public sealed class TowerCamera : MonoBehaviour
    {
        public Transform target;
        public float minY = 3.2f, maxY = 14;
        Vector3 velocity;
        void LateUpdate()
        {
            if (target == null) return;
            var destination = new Vector3(Mathf.Clamp(target.position.x * .18f, -1.2f, 1.2f), Mathf.Clamp(target.position.y + 1.1f, minY, maxY), -10);
            transform.position = Vector3.SmoothDamp(transform.position, destination, ref velocity, .18f);
        }
    }
}
