using UnityEngine;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(LightController), typeof(ShadowGenerator), typeof(LightSweep))]
    
    public sealed class DemoLightField : MonoBehaviour
    {
        [SerializeField] ChordInput playerInput;
        [SerializeField] float duration = 3;
        [SerializeField] float hearingRadius = 11;
        LightController controller;
        ShadowGenerator contour;
        // CHANGED: removed the unused "BeamSpline beam" field
        float until, strength;
        public LightColorType Kind => controller.profile.lightColor;
        public float Remaining => Mathf.Max(0, until - Time.time);
        public float Strength => strength;
        public void Configure(ChordInput input) { playerInput = input; }
        void Awake()
        {
            controller = GetComponent<LightController>();
            contour = GetComponent<ShadowGenerator>();
            // CHANGED: removed the unused beam = GetComponent<BeamSpline>() line
        }
        public void TriggerChord(bool major)
        {
            if (controller.profile == null) return; // CHANGED: stops the crash when no profile is set yet
            if ((Kind == LightColorType.Blue) || (Kind == LightColorType.Red && major))
            {
                if (playerInput != null) controller.BendAwayFrom(playerInput.transform.position);
                until = Time.time + duration;
            }
        }
        void Update()
        {
            if (controller.profile == null) return;
            if (playerInput != null && Vector2.Distance(transform.position, playerInput.transform.position) <= hearingRadius)
            {
                if (playerInput.ChordPressed || playerInput.MajorPressed) TriggerChord(playerInput.MajorPressed);
            }

            if (playerInput != null && Remaining > 0)
                controller.TrackPlayer(playerInput.transform.position);
            float target = Remaining > .4f ? 1 : Remaining / .4f;
            strength = Mathf.MoveTowards(strength, target, Time.deltaTime / .25f);
            controller.SetResonance(strength);
        }
        // The exact boundary drawn by ShadowRenderer is also used for exposure.
        // Test both containment and polygon edges against the player's world AABB.
        public bool Overlaps(Bounds bounds)
        {
            var points = contour.shadowPoints;
            if (points == null || points.Length < 3) return false;
            Vector2 center = bounds.center;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = transform.TransformPoint(points[j]);
                Vector2 b = transform.TransformPoint(points[i]);
                if ((a.y > center.y) != (b.y > center.y) && center.x < (b.x - a.x) * (center.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
                if (SegmentIntersects(bounds, a, b)) return true;
            }
            return inside;
        }
        static bool SegmentIntersects(Bounds box, Vector2 a, Vector2 b)
        {
            float low = 0, high = 1;
            Vector2 d = b - a;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(d[axis]) < .00001f)
                { if (a[axis] < box.min[axis] || a[axis] > box.max[axis]) return false; }
                else
                {
                    float x = (box.min[axis] - a[axis]) / d[axis];
                    float y = (box.max[axis] - a[axis]) / d[axis];
                    low = Mathf.Max(low, Mathf.Min(x, y)); high = Mathf.Min(high, Mathf.Max(x, y));
                    if (low > high) return false;
                }
            }
            return true;
        }
    }
}