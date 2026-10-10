using UnityEngine;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(110)]
    public sealed class TowerParallax : MonoBehaviour
    {
        public Transform cameraTransform;
        [Range(0, 1)] public float factor = .25f;
        Vector3 origin, cameraOrigin;
        void Start() { origin = transform.position; cameraOrigin = cameraTransform.position; }
        void LateUpdate()
        {
            Vector3 displacement = cameraTransform.position - cameraOrigin;
            displacement.z = 0;
            transform.position = origin + displacement * factor;
        }
    }
}
