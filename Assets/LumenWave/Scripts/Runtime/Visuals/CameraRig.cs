using Unity.Cinemachine;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Thin facade over Cinemachine 3 (kept so screenshot / showcase / finale / menu code keeps its API).
    /// The main camera carries a <see cref="CinemachineBrain"/>; this component owns one follow
    /// <see cref="CinemachineCamera"/> with:
    ///  - <see cref="CinemachinePositionComposer"/>: close framing (Umbra ~15 % of the screen, ground ~28 % from the
    ///    bottom), a slight downward pitch, a dead zone, damping and velocity look-ahead, plus a facing-based
    ///    screen offset so more of the path ahead is visible and a downward peek while falling fast;
    ///  - <see cref="CinemachineConfiner3D"/>: a trigger BoxCollider (Ignore Raycast layer, at camera depth, far from
    ///    gameplay) sized from the level bounds so the view never leaves the level;
    ///  - <see cref="CinemachineImpulseListener"/>: <see cref="Shake"/> fires a <see cref="CinemachineImpulseSource"/>.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public const float DefaultDistance = 21f;
        public const float FieldOfView = 30f;
        /// <summary>Downward pitch in degrees (small: the playfield stays side-on, platform tops just show).</summary>
        public const float Pitch = 3f;
        public static CameraRig Instance { get; private set; }

        public float distance = DefaultDistance;
        /// <summary>Screen-space forward bias (fraction of screen width) in the facing direction.</summary>
        public float lookAheadScreen = 0.15f;
        /// <summary>Feet position below screen centre (fraction of screen height, before pitch).</summary>
        public float feetScreenY = 0.21f;
        public float shakeStrength = 0.6f;

        public Transform Target { get; private set; }
        public Rect Bounds { get; private set; } = new Rect(-1000, -1000, 2000, 2000);
        /// <summary>Extra framing offset that cutscenes (e.g. finale) can set.</summary>
        public Vector2 extraOffset;
        /// <summary>Extra distance (zoom out) that cutscenes can set.</summary>
        public float extraDistance;

        public CinemachineCamera VirtualCamera { get; private set; }
        public CinemachineBrain Brain { get; private set; }

        IPlayerBody _body;
        Camera _cam;
        CinemachinePositionComposer _composer;
        CinemachineConfiner3D _confiner;
        BoxCollider _volume;
        CinemachineImpulseSource _impulse;
        Transform _proxy;
        float _screenX, _peek;
        float _volumeDistance = -1f;

        void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            Brain = GetComponent<CinemachineBrain>() ?? gameObject.AddComponent<CinemachineBrain>();
            Brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.2f);

            var vgo = new GameObject("CM FollowCamera");
            vgo.transform.SetParent(transform.parent, false);
            vgo.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(Pitch, 0f, 0f));
            VirtualCamera = vgo.AddComponent<CinemachineCamera>();
            var lens = LensSettings.Default;
            lens.FieldOfView = FieldOfView;
            lens.NearClipPlane = 0.3f;
            lens.FarClipPlane = 260f;
            VirtualCamera.Lens = lens;
            VirtualCamera.Priority = 10;

            _composer = vgo.AddComponent<CinemachinePositionComposer>();
            _composer.CameraDistance = distance;
            _composer.Damping = new Vector3(0.45f, 0.6f, 0.4f);
            var comp = ScreenComposerSettings.Default;
            comp.ScreenPosition = new Vector2(0f, feetScreenY);
            comp.DeadZone.Enabled = true;
            comp.DeadZone.Size = new Vector2(0.04f, 0.14f);
            comp.HardLimits.Enabled = true;
            comp.HardLimits.Size = new Vector2(0.7f, 0.8f);
            _composer.Composition = comp;
            _composer.Lookahead = new LookaheadSettings { Enabled = true, Time = 0.3f, Smoothing = 6f, IgnoreY = true };
            _composer.CenterOnActivate = true;

            var vol = new GameObject("CM ConfinerVolume");
            vol.transform.SetParent(transform.parent, false);
            vol.layer = 2;   // Ignore Raycast; the volume lives at camera depth, away from all gameplay colliders
            _volume = vol.AddComponent<BoxCollider>();
            _volume.isTrigger = true;
            _confiner = vgo.AddComponent<CinemachineConfiner3D>();
            _confiner.BoundingVolume = _volume;
            _confiner.SlowingDistance = 0f;

            var listener = vgo.AddComponent<CinemachineImpulseListener>();
            listener.Gain = 1f;
            listener.UseCameraSpace = true;
            _impulse = gameObject.AddComponent<CinemachineImpulseSource>();
            var def = _impulse.ImpulseDefinition;
            def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble;
            def.ImpulseDuration = 0.35f;

            _proxy = new GameObject("CM FramingProxy").transform;
            _proxy.SetParent(transform.parent, false);
            UpdateVolume(true);
        }

        void OnEnable() { if (Brain != null) Brain.enabled = true; if (VirtualCamera != null) VirtualCamera.enabled = true; }
        /// <summary>Disabling the rig (menu drift) hands the camera transform back to plain scripts.</summary>
        void OnDisable() { if (Brain != null) Brain.enabled = false; if (VirtualCamera != null) VirtualCamera.enabled = false; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (VirtualCamera != null) Destroy(VirtualCamera.gameObject);
            if (_volume != null) Destroy(_volume.gameObject);
            if (_proxy != null) Destroy(_proxy.gameObject);
        }

        public void SetTarget(Transform t)
        {
            Target = t;
            _body = t != null ? t.GetComponent<IPlayerBody>() : null;
            if (VirtualCamera != null) VirtualCamera.Follow = t != null ? t : _proxy;
        }

        public void SetBounds(Rect r)
        {
            Bounds = r;
            UpdateVolume(true);
        }

        /// <summary>Add screen shake (0..1 trauma) as a Cinemachine impulse. Ignored when disabled in settings.</summary>
        public static void Shake(float amount)
        {
            if (Instance == null || !Settings.ScreenShake || Instance._impulse == null) return;
            var dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            Instance._impulse.GenerateImpulseWithVelocity(new Vector3(dir.x, dir.y, 0f) * Mathf.Clamp01(amount) * Instance.shakeStrength);
        }

        /// <summary>Cut straight to the target (no damping) on the next camera update.</summary>
        public void SnapToTarget()
        {
            if (Target == null) TryFindTarget();
            if (Target == null) return;
            _screenX = -(_body != null ? _body.Facing : 1) * lookAheadScreen;
            _peek = 0f;
            ApplyParams();
            Cut(GoalFor(Target.position));
        }

        /// <summary>Frames a world point (used when there is no target, e.g. screenshots / cutscenes).</summary>
        public void FramePoint(Vector3 p)
        {
            _proxy.position = p;
            if (Target == null && VirtualCamera != null) VirtualCamera.Follow = _proxy;
            _screenX = 0f;
            ApplyParams();
            Cut(GoalFor(p));
        }

        void Cut(Vector3 pos)
        {
            if (VirtualCamera == null) return;
            pos = ClampToBounds(pos);
            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            VirtualCamera.PreviousStateIsValid = false;
            VirtualCamera.ForceCameraPosition(pos, rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Camera position that puts a world point where the composer would (for cuts).</summary>
        Vector3 GoalFor(Vector3 p)
        {
            float d = distance + extraDistance;
            float halfH = d * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * (_cam != null ? _cam.aspect : 16f / 9f);
            var target = p + new Vector3(extraOffset.x, extraOffset.y + _peek, 0f);
            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            // Composer screen position (x right, y down in Cinemachine, -0.5..0.5) -> offset in camera space.
            var sp = _composer != null ? _composer.Composition.ScreenPosition : Vector2.zero;
            var local = new Vector3(-sp.x * 2f * halfW, sp.y * 2f * halfH, -d);
            return target + rot * local;
        }

        void TryFindTarget()
        {
            var rt = LevelRuntime.Current;
            if (rt != null && rt.Player != null) SetTarget(rt.Player.transform);
        }

        void Update()
        {
            if (Target == null) TryFindTarget();
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (Target != null && dt > 0f)
            {
                int facing = _body != null ? _body.Facing : 1;
                _screenX = Mathf.Lerp(_screenX, -facing * lookAheadScreen, 1f - Mathf.Exp(-2.2f * dt));
                bool falling = _body != null && _body.Velocity.y < -9f;
                _peek = Mathf.Lerp(_peek, falling ? -3f : 0f, 1f - Mathf.Exp(-3f * dt));
            }
            ApplyParams();
        }

        void ApplyParams()
        {
            if (_composer == null) return;
            _composer.CameraDistance = distance + extraDistance;
            var comp = _composer.Composition;
            comp.ScreenPosition = new Vector2(_screenX, feetScreenY);
            _composer.Composition = comp;
            _composer.TargetOffset = new Vector3(extraOffset.x, extraOffset.y + _peek, 0f);
            UpdateVolume(false);
        }

        /// <summary>
        /// Confiner volume = the set of camera positions whose view stays inside <see cref="Bounds"/> at the
        /// play plane (accounting for the pitch), at the current camera distance.
        /// </summary>
        void UpdateVolume(bool force)
        {
            if (_volume == null) return;
            float d = distance + extraDistance;
            if (!force && Mathf.Abs(d - _volumeDistance) < 0.01f) return;
            _volumeDistance = d;
            float halfH = d * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * (_cam != null ? _cam.aspect : 16f / 9f);
            float drop = d * Mathf.Tan(Pitch * Mathf.Deg2Rad);
            var b = Bounds;
            float x0 = b.xMin + halfW, x1 = b.xMax - halfW;
            if (x1 < x0) x0 = x1 = b.center.x;
            float y0 = b.yMin + halfH + drop, y1 = Mathf.Max(y0, b.yMax - halfH + drop);
            _volume.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, -d);
            _volume.transform.rotation = Quaternion.identity;
            _volume.size = new Vector3(Mathf.Max(0.01f, x1 - x0), Mathf.Max(0.01f, y1 - y0), 12f);
        }

        Vector3 ClampToBounds(Vector3 pos)
        {
            if (_volume == null) return pos;
            var c = _volume.transform.position; var e = _volume.size * 0.5f;
            pos.x = Mathf.Clamp(pos.x, c.x - e.x, c.x + e.x);
            pos.y = Mathf.Clamp(pos.y, c.y - e.y, c.y + e.y);
            return pos;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
    }
}
