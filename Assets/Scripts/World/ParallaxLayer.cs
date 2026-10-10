using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Background layer that moves with the camera by a factor: 1 = glued to the camera (infinitely far),
    /// 0 = fixed in the world. Vertical uses its own factor and is anchored to the camera height at level start.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField] Transform cameraTransform;
        [Tooltip("Horizontal follow factor (0..1). Higher = further away.")]
        [SerializeField, Range(0f, 1f)] float factorX = 0.8f;
        [Tooltip("Vertical follow factor (0..1).")]
        [SerializeField, Range(0f, 1f)] float factorY = 0.9f;
        [Tooltip("Height of the layer's bottom, relative to the camera's bottom edge at the reference height.")]
        [SerializeField] float offsetY;

        float referenceCameraY;
        float halfHeight = 7.5f;

        public void Setup(Transform cam, float factorX, float factorY, float offsetY, float referenceCameraY, float viewHalfHeight)
        {
            cameraTransform = cam;
            this.factorX = factorX;
            this.factorY = factorY;
            this.offsetY = offsetY;
            this.referenceCameraY = referenceCameraY;
            halfHeight = viewHalfHeight;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (cameraTransform == null) return;
            Vector3 cam = cameraTransform.position;
            float baseY = referenceCameraY - halfHeight + offsetY;
            transform.position = new Vector3(cam.x * factorX, baseY + (cam.y - referenceCameraY) * factorY, transform.position.z);
        }
    }
}
