using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Procedural Shadow-Fight-style silhouette for Umbra: primitive limbs on joint pivots, posed every
    /// frame by a small state machine (idle, run, jump, fall, land, dash, cast, shield, hurt, death, respawn).
    /// Angles are degrees around Z; for limbs, positive swings forward (toward facing), knees bend negative.
    /// </summary>
    public class UmbraRig : MonoBehaviour
    {
        public static readonly Color RimColor = new Color(0.55f, 0.95f, 1.4f) * 1.6f;
        public static readonly Color EyeColor = new Color(0.7f, 1.6f, 2.2f) * 2.2f;

        public enum State { Idle, Run, Jump, Fall, Dash, Dead }

        public State Current { get; private set; }
        public Transform Visual { get; private set; }
        public Transform Neck => _neck;

        UmbraController _ctl;
        PlayerHealth _health;
        Material _bodyMat;
        Material _ghostMatBase;
        readonly List<Renderer> _renderers = new List<Renderer>();

        Transform _pelvis, _torso, _neck, _head;
        Transform _armNearU, _armNearL, _armFarU, _armFarL;
        Transform _legNearU, _legNearL, _legFarU, _legFarL;

        // Current joint angles (smoothed toward targets).
        float _aTorso, _aHead, _aANU, _aANL, _aAFU, _aAFL, _aLNU, _aLNL, _aLFU, _aLFL, _pelvisY;
        float _runPhase, _facingYaw, _castTimer, _hurtTimer, _deathTimer, _respawnTimer, _ghostTimer;
        bool _wasDead;
        Vector3 _squash = Vector3.one;
        static Mesh _capsule, _sphere;

        const float PelvisHeight = 0.95f;

        public void Build(UmbraController ctl)
        {
            _ctl = ctl;
            _health = GetComponent<PlayerHealth>();
            if (_capsule == null) _capsule = PrimitiveMesh(PrimitiveType.Capsule);
            if (_sphere == null) _sphere = PrimitiveMesh(PrimitiveType.Sphere);

            _bodyMat = MaterialLibrary.Create("Umbra");
            _bodyMat.name = "UmbraBody";
            if (_bodyMat.HasProperty("_BaseColor")) _bodyMat.SetColor("_BaseColor", Color.black);
            if (_bodyMat.HasProperty("_RimColor")) _bodyMat.SetColor("_RimColor", RimColor);
            if (_bodyMat.HasProperty("_RimPower")) _bodyMat.SetFloat("_RimPower", 3f);
            if (_bodyMat.HasProperty("_RimF0")) _bodyMat.SetFloat("_RimF0", 0.04f);
            var eyeMat = MaterialLibrary.Tinted("Unlit", EyeColor);

            Visual = new GameObject("Visual").transform;
            Visual.SetParent(transform, false);

            _pelvis = Joint("Pelvis", Visual, new Vector3(0f, PelvisHeight, 0f));
            Part("PelvisMesh", _sphere, _pelvis, new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.22f, 0.26f));

            _torso = Joint("Torso", _pelvis, Vector3.zero);
            Part("Abdomen", _capsule, _torso, new Vector3(0f, 0.2f, 0f), new Vector3(0.27f, 0.2f, 0.22f));
            Part("Chest", _sphere, _torso, new Vector3(0.02f, 0.44f, 0f), new Vector3(0.42f, 0.36f, 0.3f));

            _neck = Joint("Neck", _torso, new Vector3(0.03f, 0.6f, 0f));
            Part("NeckMesh", _capsule, _neck, new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.07f, 0.1f));
            _head = Joint("Head", _neck, new Vector3(0.02f, 0.1f, 0f));
            Part("Skull", _sphere, _head, new Vector3(0f, 0.11f, 0f), new Vector3(0.26f, 0.3f, 0.26f));
            Part("Jaw", _sphere, _head, new Vector3(0.05f, 0.03f, 0f), new Vector3(0.16f, 0.12f, 0.2f));
            // Eyes: thin glowing slits, near eye toward the camera (−Z).
            Part("EyeNear", _sphere, _head, new Vector3(0.11f, 0.13f, -0.07f), new Vector3(0.07f, 0.028f, 0.03f), eyeMat, false);
            Part("EyeFar", _sphere, _head, new Vector3(0.11f, 0.13f, 0.07f), new Vector3(0.07f, 0.028f, 0.03f), eyeMat, false);

            float zn = -0.13f, zf = 0.13f;
            _armNearU = Limb("ArmNearU", _torso, new Vector3(0f, 0.52f, zn), 0.32f, 0.12f);
            _armNearL = Limb("ArmNearL", _armNearU, new Vector3(0f, -0.32f, 0f), 0.31f, 0.1f);
            Part("HandNear", _sphere, _armNearL, new Vector3(0f, -0.34f, 0f), new Vector3(0.11f, 0.12f, 0.1f));
            _armFarU = Limb("ArmFarU", _torso, new Vector3(0f, 0.52f, zf), 0.32f, 0.12f);
            _armFarL = Limb("ArmFarL", _armFarU, new Vector3(0f, -0.32f, 0f), 0.31f, 0.1f);
            Part("HandFar", _sphere, _armFarL, new Vector3(0f, -0.34f, 0f), new Vector3(0.11f, 0.12f, 0.1f));

            _legNearU = Limb("LegNearU", _pelvis, new Vector3(0f, -0.02f, zn * 0.7f), 0.47f, 0.17f);
            _legNearL = Limb("LegNearL", _legNearU, new Vector3(0f, -0.47f, 0f), 0.46f, 0.13f);
            Part("FootNear", _sphere, _legNearL, new Vector3(0.07f, -0.47f, 0f), new Vector3(0.24f, 0.08f, 0.12f));
            _legFarU = Limb("LegFarU", _pelvis, new Vector3(0f, -0.02f, zf * 0.7f), 0.47f, 0.17f);
            _legFarL = Limb("LegFarL", _legFarU, new Vector3(0f, -0.47f, 0f), 0.46f, 0.13f);
            Part("FootFar", _sphere, _legFarL, new Vector3(0.07f, -0.47f, 0f), new Vector3(0.24f, 0.08f, 0.12f));

            _ghostMatBase = MaterialLibrary.Create("UnlitTransparent");
            _facingYaw = 0f;
        }

        static Mesh PrimitiveMesh(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            return m;
        }

        Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        Transform Limb(string name, Transform parent, Vector3 localPos, float length, float thickness)
        {
            var j = Joint(name, parent, localPos);
            Part(name + "Mesh", _capsule, j, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.5f + thickness * 0.25f, thickness));
            return j;
        }

        void Part(string name, Mesh mesh, Transform parent, Vector3 localPos, Vector3 scale, Material mat = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.layer = Layers.Player;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat != null ? mat : _bodyMat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _renderers.Add(r);
        }

        void OnEnable()
        {
            GameEvents.PowerUsed += OnPower;
            GameEvents.PlayerDamaged += OnDamaged;
            GameEvents.PlayerLanded += OnLanded;
        }

        void OnDisable()
        {
            GameEvents.PowerUsed -= OnPower;
            GameEvents.PlayerDamaged -= OnDamaged;
            GameEvents.PlayerLanded -= OnLanded;
        }

        void OnPower(PowerType p)
        {
            if (p == PowerType.Echo || p == PowerType.Pulse) _castTimer = 0.38f;
        }

        void OnDamaged(float amount, LightColor c) { _hurtTimer = 0.25f; }

        void OnLanded()
        {
            if (_ctl == null) return;
            float s = Mathf.Clamp01(_ctl.LastLandingSpeed / 18f);
            _squash = new Vector3(1f + 0.25f * s, 1f - 0.3f * s, 1f);
            if (s > 0.45f) UmbraFx.Dust(transform.position, s);
        }

        void LateUpdate()
        {
            if (_ctl == null || Visual == null) return;
            float dt = Time.deltaTime;
            bool dead = _health != null && _health.IsDead;
            var v = _ctl.Velocity;
            var cmd = _ctl.Current;

            // State selection.
            if (dead) Current = State.Dead;
            else if (_ctl.IsDashing) Current = State.Dash;
            else if (!_ctl.Grounded) Current = v.y > 0.5f ? State.Jump : State.Fall;
            else Current = Mathf.Abs(v.x) > 0.4f ? State.Run : State.Idle;

            // Death / respawn transitions.
            if (dead && !_wasDead) { _deathTimer = 0f; UmbraFx.Burst(transform.position + Vector3.up * 0.9f, RimColor); }
            if (!dead && _wasDead) _respawnTimer = 0.45f;
            _wasDead = dead;

            float speedN = Mathf.Clamp01(Mathf.Abs(v.x) / Mathf.Max(0.01f, _ctl.maxSpeed));
            float t = Time.time;

            // Targets.
            float tTorso = 0, tHead = 0, tANU = 6, tANL = 12, tAFU = -6, tAFL = 12, tLNU = 3, tLNL = -4, tLFU = -3, tLFL = -4, tPelvis = 0;
            switch (Current)
            {
                case State.Idle:
                {
                    float br = Mathf.Sin(t * 2.2f);
                    tTorso = 2f + br * 1.5f; tHead = -br * 2f;
                    tANU = 8 + br * 3; tAFU = -5 - br * 3; tANL = 14; tAFL = 10;
                    tPelvis = -0.01f + br * 0.008f;
                    break;
                }
                case State.Run:
                {
                    _runPhase += dt * Mathf.Abs(v.x) / 2.3f * Mathf.PI * 2f;
                    float s = Mathf.Sin(_runPhase), sf = Mathf.Sin(_runPhase + Mathf.PI);
                    tTorso = -9f * speedN - 3f; tHead = 6f * speedN;
                    tLNU = s * 42f * speedN + 4; tLFU = sf * 42f * speedN + 4;
                    tLNL = -Mathf.Max(0f, Mathf.Sin(_runPhase - 1.3f)) * 80f * speedN - 10f;
                    tLFL = -Mathf.Max(0f, Mathf.Sin(_runPhase + Mathf.PI - 1.3f)) * 80f * speedN - 10f;
                    tANU = -s * 38f * speedN + 8; tAFU = -sf * 38f * speedN + 8;
                    tANL = 35f + Mathf.Max(0f, -s) * 35f; tAFL = 35f + Mathf.Max(0f, -sf) * 35f;
                    tPelvis = -0.05f * speedN + Mathf.Abs(Mathf.Cos(_runPhase)) * 0.07f * speedN;
                    break;
                }
                case State.Jump:
                    tTorso = -6; tHead = 8;
                    tLNU = 55; tLNL = -85; tLFU = -12; tLFL = -40;
                    tANU = 150; tANL = 20; tAFU = -40; tAFL = 30;
                    break;
                case State.Fall:
                {
                    float w = Mathf.Sin(t * 14f) * 6f;
                    tTorso = 4; tHead = -4;
                    tLNU = 22; tLNL = -30; tLFU = -18; tLFL = -45;
                    tANU = 110 + w; tANL = 25; tAFU = 70 - w; tAFL = 25;
                    break;
                }
                case State.Dash:
                    tTorso = -32; tHead = 20;
                    tLNU = 38; tLNL = -70; tLFU = -55; tLFL = -25;
                    tANU = -75; tANL = 10; tAFU = -65; tAFL = 10;
                    tPelvis = -0.12f;
                    break;
                case State.Dead:
                    tTorso = 20; tHead = 35; tLNU = 30; tLNL = -90; tLFU = 10; tLFL = -80;
                    tANU = 60; tANL = 70; tAFU = 40; tAFL = 60; tPelvis = -0.4f;
                    break;
            }

            // Overlays: casting thrust, holding the shield (both arms raised), landing crouch.
            if (!dead)
            {
                if (cmd.shieldHeld)
                {
                    tANU = 155; tANL = 25; tAFU = 140; tAFL = 30; tTorso += 6; tHead -= 8;
                }
                else if (_castTimer > 0f)
                {
                    float k = _castTimer / 0.38f;
                    tANU = Mathf.Lerp(90f, 60f, 1f - k); tANL = 0f; tAFU = -30f; tTorso -= 10f * k; tHead += 4f;
                }
                if (_ctl.Grounded && _ctl.TimeSinceLanded < 0.12f)
                {
                    tPelvis -= 0.12f; tLNU += 30; tLNL -= 50; tLFU += 25; tLFL -= 45;
                }
            }

            float k1 = 1f - Mathf.Exp(-dt * (Current == State.Run ? 30f : 16f));
            _aTorso = Mathf.LerpAngle(_aTorso, tTorso, k1);
            _aHead = Mathf.LerpAngle(_aHead, tHead, k1);
            _aANU = Mathf.LerpAngle(_aANU, tANU, k1); _aANL = Mathf.LerpAngle(_aANL, tANL, k1);
            _aAFU = Mathf.LerpAngle(_aAFU, tAFU, k1); _aAFL = Mathf.LerpAngle(_aAFL, tAFL, k1);
            _aLNU = Mathf.LerpAngle(_aLNU, tLNU, k1); _aLNL = Mathf.LerpAngle(_aLNL, tLNL, k1);
            _aLFU = Mathf.LerpAngle(_aLFU, tLFU, k1); _aLFL = Mathf.LerpAngle(_aLFL, tLFL, k1);
            _pelvisY = Mathf.Lerp(_pelvisY, tPelvis, k1);

            _pelvis.localPosition = new Vector3(0f, PelvisHeight + _pelvisY, 0f);
            _torso.localRotation = Quaternion.Euler(0, 0, _aTorso);
            _head.localRotation = Quaternion.Euler(0, 0, _aHead);
            _armNearU.localRotation = Quaternion.Euler(0, 0, _aANU); _armNearL.localRotation = Quaternion.Euler(0, 0, _aANL);
            _armFarU.localRotation = Quaternion.Euler(0, 0, _aAFU); _armFarL.localRotation = Quaternion.Euler(0, 0, _aAFL);
            _legNearU.localRotation = Quaternion.Euler(0, 0, _aLNU); _legNearL.localRotation = Quaternion.Euler(0, 0, _aLNL);
            _legFarU.localRotation = Quaternion.Euler(0, 0, _aLFU); _legFarL.localRotation = Quaternion.Euler(0, 0, _aLFL);
            // Breathing chest
            _torso.localScale = new Vector3(1f, 1f + (Current == State.Idle ? Mathf.Sin(t * 2.2f) * 0.015f : 0f), 1f);

            // Facing: quick 3D turn (yaw) — near/far limbs swap naturally.
            float targetYaw = _ctl.Facing > 0 ? 0f : 180f;
            _facingYaw = Mathf.MoveTowardsAngle(_facingYaw, targetYaw, dt * 1600f);

            // Squash & stretch.
            Vector3 targetScale = Vector3.one;
            if (Current == State.Dash) targetScale = new Vector3(1.22f, 0.88f, 1f);
            else if (Current == State.Jump) targetScale = new Vector3(0.92f, 1.08f, 1f);
            _squash = Vector3.Lerp(_squash, targetScale, 1f - Mathf.Exp(-dt * 14f));

            float lifeScale = 1f;
            if (dead)
            {
                _deathTimer += dt;
                lifeScale = Mathf.Clamp01(1f - (_deathTimer - 0.25f) / 0.6f);
            }
            else if (_respawnTimer > 0f)
            {
                _respawnTimer -= dt;
                lifeScale = 1f - Mathf.Clamp01(_respawnTimer / 0.45f);
            }
            Visual.localRotation = Quaternion.Euler(0f, _facingYaw, 0f);
            // Squash is in world X/Y — apply before yaw so it doesn't flip.
            Visual.localScale = new Vector3(_squash.x, _squash.y, 1f) * Mathf.Max(0.001f, lifeScale);

            // Rim colour: hurt flash red, respawn flash bright.
            _castTimer -= dt; _hurtTimer -= dt;
            if (_bodyMat != null && _bodyMat.HasProperty("_RimColor"))
            {
                Color rim = RimColor;
                if (_hurtTimer > 0f) rim = Color.Lerp(RimColor, new Color(3f, 0.3f, 0.2f), _hurtTimer / 0.25f);
                if (_respawnTimer > 0f) rim = Color.Lerp(RimColor, RimColor * 3f, _respawnTimer / 0.45f);
                if (cmd.shieldHeld && !dead) rim = Color.Lerp(rim, new Color(0.9f, 1.3f, 2.2f) * 2f, 0.5f);
                _bodyMat.SetColor("_RimColor", rim);
            }

            // Dash afterimages.
            if (Current == State.Dash)
            {
                _ghostTimer -= dt;
                if (_ghostTimer <= 0f) { _ghostTimer = 0.035f; SpawnGhost(); }
            }
            else _ghostTimer = 0f;
        }

        void SpawnGhost()
        {
            var ghost = Instantiate(Visual.gameObject, Visual.position, Visual.rotation);
            ghost.name = "UmbraGhost";
            ghost.transform.localScale = Visual.lossyScale;
            var mat = new Material(_ghostMatBase);
            var c = new Color(0.35f, 0.9f, 1.3f, 0.4f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ghost.AddComponent<UmbraGhost>().Init(mat, c, 0.28f);
        }

        void OnDestroy()
        {
            if (_bodyMat != null) Destroy(_bodyMat);
            if (_ghostMatBase != null) Destroy(_ghostMatBase);
        }
    }

    /// <summary>Fading afterimage left behind by a dash.</summary>
    public class UmbraGhost : MonoBehaviour
    {
        Material _mat; Color _c; float _life, _t;
        public void Init(Material m, Color c, float life) { _mat = m; _c = c; _life = life; }
        void Update()
        {
            _t += Time.deltaTime;
            float a = Mathf.Clamp01(1f - _t / _life);
            if (_mat != null && _mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", new Color(_c.r, _c.g, _c.b, _c.a * a));
            if (_t >= _life) { Destroy(gameObject); if (_mat != null) Destroy(_mat); }
        }
    }

    /// <summary>Small one-shot particle effects for Umbra (death burst, landing dust).</summary>
    public static class UmbraFx
    {
        public static void Burst(Vector3 pos, Color color)
        {
            var ps = Make("UmbraBurst", pos, MaterialLibrary.Tinted("ParticleAdditive", Color.white), 1.4f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, new Color(0.05f, 0.05f, 0.08f));
            main.gravityModifier = -0.15f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.5f;
            ps.Emit(60);
        }

        public static void Dust(Vector3 pos, float strength)
        {
            var ps = Make("UmbraDust", pos + Vector3.up * 0.05f, MaterialLibrary.Tinted("ParticleAlpha", Color.white), 1f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new Color(0.25f, 0.3f, 0.38f, 0.55f);
            main.gravityModifier = 0.2f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 80f; shape.radius = 0.2f;
            ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
            ps.Emit(Mathf.RoundToInt(8 + 14 * strength));
        }

        static ParticleSystem Make(string name, Vector3 pos, Material mat, float life)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            Object.Destroy(go, life + 1.5f);
            return ps;
        }
    }
}
