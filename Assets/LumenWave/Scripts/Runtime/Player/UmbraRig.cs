using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Umbra's body: the skinned UAL mannequin (<see cref="UmbraHero"/>, prefab Resources/Hero/UmbraModel
    /// built by EditorTools.HeroSetup) animated only by its Animator Controller. This component owns the look
    /// around it: the black rim material and stencil outline, rim colour (hit flash, shield, singing),
    /// glowing-eye materials, whole-body squash on landing, death/respawn timers and dash ghosts.
    /// There is no procedural fallback body: if the prefab is missing an error is logged and Umbra is invisible.
    /// </summary>
    public class UmbraRig : MonoBehaviour
    {
        public static readonly Color RimColor = new Color(0.7f, 0.95f, 1.25f) * 1.5f;
        public static readonly Color EyeColor = new Color(0.7f, 1.6f, 2.2f) * 2.2f;

        public enum State { Idle, Run, Jump, Fall, Dash, Dead }

        public State Current { get; private set; }
        public Transform Visual { get; private set; }
        /// <summary>Neck bone of the hero (scarf anchor); the Visual root if the hero failed to load.</summary>
        public Transform Neck => _neck != null ? _neck : Visual;
        /// <summary>0..1: finale singing (Animator "Sing" state, golden rim, bright eyes). Set by FinaleDirector.</summary>
        public float Singing { get; set; }
        /// <summary>The skinned hero body (null only if Resources/Hero/UmbraModel is missing).</summary>
        public UmbraHero Hero => _hero;

        UmbraHero _hero;
        UmbraController _ctl;
        PlayerHealth _health;
        Material _bodyMat, _outlineMat, _ghostMatBase, _eyeMat, _haloMat;
        Transform _neck;
        float _castTimer, _hurtTimer, _deathTimer, _respawnTimer, _ghostTimer, _keyX = -0.5f;
        bool _wasDead;
        Vector3 _squash = Vector3.one;

        const float HeroRimPower = 5f, HeroRimStrength = 0.22f;
        static readonly Color HeroAmbient = new Color(0.006f, 0.008f, 0.013f);

        public void Build(UmbraController ctl)
        {
            _ctl = ctl;
            _health = GetComponent<PlayerHealth>();
            _ghostMatBase = MaterialLibrary.Create("UnlitTransparent");
            Visual = new GameObject("Visual").transform;
            Visual.SetParent(transform, false);

            GameObject prefab = null;
            try { prefab = Resources.Load<GameObject>("Hero/UmbraModel"); } catch (System.Exception e) { Debug.LogError("[LumenWave] Hero load failed: " + e.Message); }
            if (prefab == null) { Debug.LogError("[LumenWave] Missing Resources/Hero/UmbraModel (run scripts/compile.ps1 -Setup)"); return; }

            // Black body with a crisp light-facing Schlick rim, plus the stencil outline hull: strong and wider
            // on the key-light (upper/back) edge, faint on the other side.
            _bodyMat = MaterialLibrary.Create("Umbra");
            _bodyMat.name = "UmbraHeroBody";
            _bodyMat.SetColor("_BaseColor", new Color(0.004f, 0.004f, 0.007f));
            _bodyMat.SetColor("_RimColor", RimColor);
            _bodyMat.SetFloat("_RimPower", HeroRimPower);
            _bodyMat.SetFloat("_RimF0", 0f);
            if (_bodyMat.HasProperty("_RimStrength")) _bodyMat.SetFloat("_RimStrength", HeroRimStrength);
            if (_bodyMat.HasProperty("_KeyAmbient")) _bodyMat.SetFloat("_KeyAmbient", 0.03f);
            if (_bodyMat.HasProperty("_Ambient")) _bodyMat.SetColor("_Ambient", HeroAmbient);
            if (_bodyMat.HasProperty("_FogAmount")) _bodyMat.SetFloat("_FogAmount", 0.25f);
            var outlineBase = Resources.Load<Material>("Materials/UmbraOutline");
            if (outlineBase != null)
            {
                _outlineMat = new Material(outlineBase) { name = "UmbraOutline" };
                _outlineMat.SetColor("_RimColor", RimColor);
                if (_outlineMat.HasProperty("_Ambient")) _outlineMat.SetFloat("_Ambient", 0.09f);
                if (_outlineMat.HasProperty("_KeyPower")) _outlineMat.SetFloat("_KeyPower", 5f);
                if (_outlineMat.HasProperty("_WidthUnlit")) _outlineMat.SetFloat("_WidthUnlit", 0.45f);
                if (_outlineMat.HasProperty("_Width")) _outlineMat.SetFloat("_Width", 0.028f);
            }
            _eyeMat = MaterialLibrary.Create("Glow"); _eyeMat.name = "UmbraEye";
            _eyeMat.SetColor("_Color", EyeColor * 0.75f);
            if (_eyeMat.HasProperty("_Falloff")) _eyeMat.SetFloat("_Falloff", 1.6f);
            _haloMat = MaterialLibrary.Create("Glow"); _haloMat.name = "UmbraEyeHalo";
            _haloMat.SetColor("_Color", EyeColor * 0.05f);
            if (_haloMat.HasProperty("_Falloff")) _haloMat.SetFloat("_Falloff", 2.5f);

            _hero = UmbraHero.Create(prefab, Visual, this, _ctl, _bodyMat, _eyeMat, _haloMat);
            if (_hero == null) return;
            SetOutline(true);
            _neck = _hero.NeckBone;
        }

        SkinnedMeshRenderer _outlineSmr;

        /// <summary>
        /// The stencil outline hull is a second SkinnedMeshRenderer that shares the hero's mesh and bones with
        /// the outline material on every submesh (an extra material slot would only draw the last submesh).
        /// </summary>
        void SetOutline(bool on)
        {
            if (_hero == null) return;
            var body = _hero.Body;
            int n = Mathf.Max(1, body.sharedMesh.subMeshCount);
            if (body.sharedMaterials.Length != n)
            {
                var mats = new Material[n];
                for (int i = 0; i < n; i++) mats[i] = _bodyMat;
                body.sharedMaterials = mats;
            }
            if (_outlineMat == null) return;
            if (_outlineSmr == null && on)
            {
                var go = new GameObject("Outline");
                go.layer = Layers.Player;
                go.transform.SetParent(body.transform.parent, false);
                go.transform.localPosition = body.transform.localPosition;
                go.transform.localRotation = body.transform.localRotation;
                go.transform.localScale = body.transform.localScale;
                _outlineSmr = go.AddComponent<SkinnedMeshRenderer>();
                _outlineSmr.sharedMesh = body.sharedMesh;
                _outlineSmr.rootBone = body.rootBone;
                _outlineSmr.bones = body.bones;
                _outlineSmr.updateWhenOffscreen = true;
                _outlineSmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _outlineSmr.receiveShadows = false;
                _outlineSmr.skinnedMotionVectors = false;
                var om = new Material[n];
                for (int i = 0; i < n; i++) om[i] = _outlineMat;
                _outlineSmr.sharedMaterials = om;
            }
            if (_outlineSmr != null) _outlineSmr.enabled = on;
        }

        void OnEnable()
        {
            GameEvents.PowerUsed += OnPower;
            GameEvents.PlayerDamaged += OnDamaged;
            GameEvents.PlayerLanded += OnLanded;
            GameEvents.PlayerHealed += OnHealed;
        }

        void OnDisable()
        {
            GameEvents.PowerUsed -= OnPower;
            GameEvents.PlayerDamaged -= OnDamaged;
            GameEvents.PlayerLanded -= OnLanded;
            GameEvents.PlayerHealed -= OnHealed;
        }

        void OnPower(PowerType p)
        {
            if (p == PowerType.Echo || p == PowerType.Pulse) { _castTimer = 0.38f; if (_hero != null) _hero.OnCast(); }
        }

        float _sparkCooldown, _healCooldown;

        void OnDamaged(float amount, LightColor c)
        {
            _hurtTimer = 0.25f;
            if (_hero != null) _hero.OnHurt();
            // Coloured sparks in the colour of the light that burned us (throttled).
            if (Time.time < _sparkCooldown) return;
            _sparkCooldown = Time.time + 0.12f;
            var col = c == LightColor.None ? new Color(1.6f, 1.6f, 1.8f) : LightMath.DisplayColor(c) * 2.2f;
            UmbraFx.Sparks(transform.position + Vector3.up * 1.0f, col, Mathf.Clamp(Mathf.RoundToInt(amount * 1.5f), 6, 24));
        }

        void OnHealed(float amount)
        {
            if (Time.time < _healCooldown) return;
            _healCooldown = Time.time + 0.18f;
            UmbraFx.Motes(transform.position + Vector3.up * 0.6f, LightMath.DisplayColor(LightColor.Green) * 1.8f);
        }

        void OnLanded()
        {
            if (_ctl == null) return;
            // Whole-body squash on hard landings (the land dust comes from the Jump_Land animation event).
            float s = Mathf.Clamp01(_ctl.LastLandingSpeed / 18f);
            _squash = new Vector3(1f + 0.12f * s, 1f - 0.14f * s, 1f);
        }

        void LateUpdate()
        {
            if (_ctl == null || Visual == null) return;
            float dt = Time.deltaTime;
            bool dead = _health != null && _health.IsDead;
            var v = _ctl.Velocity;
            var cmd = _ctl.Current;

            if (dead) Current = State.Dead;
            else if (_ctl.IsDashing) Current = State.Dash;
            else if (!_ctl.Grounded) Current = v.y > 0.5f ? State.Jump : State.Fall;
            else Current = Mathf.Abs(v.x) > 0.4f ? State.Run : State.Idle;

            if (dead && !_wasDead)
            {
                _deathTimer = 0f;
                UmbraFx.Burst(transform.position + Vector3.up * 0.9f, RimColor);
                if (_hero != null) UmbraFx.Rise(transform.position, RimColor * 0.9f, _ctl.height, 0.9f);
            }
            if (!dead && _wasDead) _respawnTimer = 0.45f;
            _wasDead = dead;
            if (_hero == null) return;

            _squash = Vector3.Lerp(_squash, Vector3.one, 1f - Mathf.Exp(-dt * 12f));
            if (_respawnTimer > 0f) _respawnTimer -= dt;
            if (dead) _deathTimer += dt;
            _hero.LateTick(dt, Visual, _squash, _bodyMat);

            // Key light on Umbra's upper-back side; it swings over smoothly when Umbra turns.
            _keyX = Mathf.MoveTowards(_keyX, -_ctl.Facing * 0.5f, dt * 4f);
            var key = new Vector4(_keyX, 0.85f, 0f, 0f);
            _bodyMat.SetVector("_KeyDir", key);
            if (_outlineMat != null) _outlineMat.SetVector("_KeyDir", key);

            // The outline hull would outline the holes of the dissolve: drop it while dissolving.
            bool wantOutline = _hero.Dissolve < 0.02f && _outlineMat != null;
            if (_outlineSmr != null && wantOutline != _outlineSmr.enabled) SetOutline(wantOutline);

            UpdateBurn(dt, dead);
            _castTimer -= dt; _hurtTimer -= dt;
            ApplyRim(dead, cmd, Time.time);
            if (_bodyMat.HasProperty("_DissolveEdge"))
                _bodyMat.SetColor("_DissolveEdge", dead ? new Color(1.2f, 2.2f, 3.4f) : new Color(1.6f, 2.6f, 3.6f) * 1.4f);
            UpdateGhosts(dt);
        }

        /// <summary>Rim colour: hurt flash (white core, then red), respawn flash bright, shield cool, singing gold.</summary>
        void ApplyRim(bool dead, PlayerCommands cmd, float t)
        {
            if (_bodyMat == null) return;
            Color rim = RimColor;
            // Burning in red/yellow light: rim glows orange-red, stronger with damage per second.
            if (_burn > 0.001f) rim = Color.Lerp(RimColor, BurnRim * (0.85f + 0.15f * Mathf.Sin(t * 23f)), Mathf.Clamp01(_burn));
            if (_hurtTimer > 0f)
            {
                float k = _hurtTimer / 0.25f;
                var red = new Color(3f, 0.3f, 0.2f);
                var hurt = k > 0.7f ? Color.Lerp(red, new Color(4f, 3.6f, 3.4f), (k - 0.7f) / 0.3f) : red;
                rim = Color.Lerp(RimColor, hurt, Mathf.Clamp01(k * 1.3f));
            }
            if (_respawnTimer > 0f) rim = Color.Lerp(RimColor, RimColor * 3f, _respawnTimer / 0.45f);
            if (cmd.shieldHeld && !dead) rim = Color.Lerp(rim, new Color(0.9f, 1.3f, 2.2f) * 2f, 0.5f);
            if (Singing > 0.001f)
            {
                rim = Color.Lerp(rim, new Color(2.0f, 1.6f, 1.05f) * (0.9f + 0.25f * Mathf.Sin(t * 5.2f)), Mathf.Clamp01(Singing));
                var eyeC = Color.Lerp(EyeColor, new Color(2.6f, 2.3f, 1.7f) * 3f, Mathf.Clamp01(Singing));
                if (_eyeMat != null) _eyeMat.SetColor("_Color", eyeC * 0.75f);
                if (_haloMat != null) _haloMat.SetColor("_Color", eyeC * Mathf.Lerp(0.05f, 0.07f, Mathf.Clamp01(Singing)));
            }
            _bodyMat.SetColor("_RimColor", rim);
            if (_bodyMat.HasProperty("_Ambient"))
                _bodyMat.SetColor("_Ambient", _hurtTimer > 0f ? Color.Lerp(HeroAmbient, new Color(0.28f, 0.03f, 0.02f), Mathf.Clamp01(_hurtTimer / 0.25f))
                                              : Color.Lerp(HeroAmbient, new Color(0.09f, 0.025f, 0.005f), Mathf.Clamp01(_burn)));
            if (_outlineMat != null) _outlineMat.SetColor("_RimColor", rim);
        }

        // ---- Burning (red / yellow light) -------------------------------------------------------------
        static readonly Color BurnRim = new Color(2.3f, 0.42f, 0.07f);
        LightReceiver _receiver;
        float _burn;
        ParticleSystem _smoke, _embers;

        /// <summary>
        /// Burn level from the light the receiver measures (exposure-based, so it also shows in god mode):
        /// rises with damage per second, fades out within ~0.4 s after leaving the light.
        /// </summary>
        void UpdateBurn(float dt, bool dead)
        {
            if (_receiver == null) _receiver = GetComponent<LightReceiver>();
            float dps = (!dead && _receiver != null) ? _receiver.LastEffect.damagePerSecond : 0f;
            float target = dps > 0.01f ? Mathf.Clamp01(0.75f + dps / 120f) : 0f;
            _burn = target > _burn ? Mathf.MoveTowards(_burn, target, dt * 8f) : Mathf.MoveTowards(_burn, target, dt / 0.4f);
            if (_burn > 0.001f && _smoke == null) BuildBurnFx();
            if (_smoke == null) return;
            var se = _smoke.emission; se.rateOverTime = 34f * _burn;
            var ee = _embers.emission; ee.rateOverTime = 45f * _burn;
        }

        void BuildBurnFx()
        {
            var parent = _hero != null && _hero.Animator != null ? _hero.Animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            float h = _ctl.height;
            _smoke = BurnSystem("BurnSmoke", parent, h, false);
            _embers = BurnSystem("BurnEmbers", parent, h, true);
        }

        ParticleSystem BurnSystem(string name, Transform bone, float h, bool embers)
        {
            var go = new GameObject(name);
            go.layer = Layers.Player;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            if (bone != null) { var f = go.AddComponent<UmbraFollow>(); f.target = bone; }
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = embers ? 60 : 40;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.45f, h * 0.75f, 0.25f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (embers)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(3f, 1.1f, 0.25f), new Color(2.4f, 0.5f, 0.1f));
                main.gravityModifier = -0.35f;
                var noise = ps.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 2f;
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.2f), 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                r.sharedMaterial = EnemyUtil.GlowMaterial(Color.white);
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.2f;
            }
            else
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.29f, 0.27f, 0.6f), new Color(0.45f, 0.4f, 0.37f, 0.45f));
                main.gravityModifier = -0.12f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                var m = MaterialLibrary.Create("ParticleAlpha");
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", EnemyUtil.SoftDot);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", EnemyUtil.SoftDot);
                r.sharedMaterial = m;
                r.sortingFudge = 2f;
            }
            col.color = g;
            ps.Play();
            return ps;
        }

        void UpdateGhosts(float dt)
        {
            if (Current == State.Dash)
            {
                _ghostTimer -= dt;
                if (_ghostTimer <= 0f) { _ghostTimer = 0.035f; SpawnGhost(); }
            }
            else _ghostTimer = 0f;
        }

        /// <summary>Rim-only afterimage from a baked snapshot of the skinned mesh (one draw, no bones).</summary>
        void SpawnGhost()
        {
            if (_hero == null) return;
            var mesh = _hero.BakeSnapshot(out var m);
            var ghost = new GameObject("UmbraGhost");
            ghost.layer = Layers.Player;
            ghost.transform.SetPositionAndRotation(m.GetPosition(), m.rotation);
            ghost.transform.localScale = m.lossyScale;
            ghost.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = ghost.AddComponent<MeshRenderer>();
            var ghostBase = Resources.Load<Material>("Materials/UmbraGhost");
            var mat = new Material(ghostBase != null ? ghostBase : _ghostMatBase);
            var c = ghostBase != null ? new Color(0.4f, 1.2f, 2.2f, 0.8f) : new Color(0.5f, 1.2f, 2.0f, 0.45f);
            if (ghostBase != null) { mat.SetFloat("_RimPower", 3.2f); mat.SetFloat("_Fill", 0.015f); }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            var subs = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[subs];
            for (int i = 0; i < subs; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var g = ghost.AddComponent<UmbraGhost>();
            g.Init(mat, c, 0.25f);
            g.OwnedMesh = mesh;
        }

        void OnDestroy()
        {
            if (_bodyMat != null) Destroy(_bodyMat);
            if (_outlineMat != null) Destroy(_outlineMat);
            if (_ghostMatBase != null) Destroy(_ghostMatBase);
            if (_eyeMat != null) Destroy(_eyeMat);
            if (_haloMat != null) Destroy(_haloMat);
        }
    }

    /// <summary>Keeps an effect emitter on a bone's position (no rotation, no limb control).</summary>
    public class UmbraFollow : MonoBehaviour
    {
        public Transform target;
        void LateUpdate() { if (target != null) transform.position = target.position + Vector3.up * 0.15f; }
    }

    /// <summary>Fading afterimage left behind by a dash.</summary>
    public class UmbraGhost : MonoBehaviour
    {
        Material _mat; Color _c; float _life, _t;
        /// <summary>Optional baked mesh destroyed with the ghost (skinned hero snapshots).</summary>
        public Mesh OwnedMesh;
        public void Init(Material m, Color c, float life) { _mat = m; _c = c; _life = life; }
        void OnDestroy() { if (OwnedMesh != null) Destroy(OwnedMesh); }
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
            var ps = Make("UmbraBurst", pos, MaterialLibrary.Get("GlowParticle"), 1.4f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, new Color(0.05f, 0.05f, 0.08f));
            main.gravityModifier = -0.15f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.5f;
            ps.Emit(60);
        }

        /// <summary>Fast coloured sparks flying off Umbra when light burns them.</summary>
        public static void Sparks(Vector3 pos, Color color, int count)
        {
            var ps = Make("UmbraSparks", pos, MaterialLibrary.Tinted("ParticleAdditive", Color.white), 0.6f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = color;
            main.gravityModifier = 0.6f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.35f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.5f;
            ps.Emit(count);
        }

        /// <summary>Soft motes drifting upward while green light heals.</summary>
        public static void Motes(Vector3 pos, Color color)
        {
            var ps = Make("UmbraHeal", pos, MaterialLibrary.Get("GlowParticle"), 1.2f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = color;
            main.gravityModifier = -0.25f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.6f, 1.2f, 0.3f);
            ps.Emit(4);
        }

        /// <summary>Motes rising off a dissolving body (death): emitted over <paramref name="duration"/> seconds.</summary>
        public static void Rise(Vector3 feet, Color color, float height, float duration)
        {
            var ps = Make("UmbraRise", feet + Vector3.up * height * 0.5f, MaterialLibrary.Get("GlowParticle"), duration + 1.6f);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, color * 0.4f);
            main.gravityModifier = -0.35f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.45f, height * 0.9f, 0.25f);
            var em = ps.emission; em.enabled = true; em.rateOverTime = 70f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 1.2f;
            ps.Play();
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
