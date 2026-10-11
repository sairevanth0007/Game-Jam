using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Umbra's body: the Quaternius UAL mannequin (Humanoid) driven purely by its Animator Controller
    /// (Resources/Hero/UmbraAnimator.controller, built by EditorTools.HeroSetup). All limb motion comes from
    /// animation clips. This component only:
    ///  - feeds the Animator parameters from gameplay state (Speed, VerticalSpeed, Grounded, Dash, Cast,
    ///    Shield, Hurt, Dead, Sing, LocoRate);
    ///  - turns the whole model to face ±X with a 3/4 turn toward the camera (root rotation only);
    ///  - drives the rim/dissolve material, the glowing eyes (quads parented to the Head bone) and dash snapshots.
    /// </summary>
    public class UmbraHero : MonoBehaviour
    {
        /// <summary>Rendered height relative to the controller's collider height.</summary>
        public const float HeightFactor = 1.06f;
        /// <summary>Extra girth (static model X/Z scale) so the mannequin reads as a sturdy silhouette.</summary>
        public const float WidthFactor = 1.1f;
        const float ThreeQuarterTurn = 22f;

        public SkinnedMeshRenderer Body { get; private set; }
        public Transform NeckBone { get; private set; }
        public Transform HeadBone { get; private set; }
        public Animator Animator => _anim;
        public float Dissolve => _dissolve;
        public float Height => _ctl != null ? _ctl.height * HeightFactor : 1.8f;

        UmbraRig _rig;
        UmbraController _ctl;
        PlayerHealth _health;
        Animator _anim;
        float _yaw, _dissolve, _blinkTimer, _blink = 1f, _nextBlink, _hurtCooldown;
        bool _wasDead;
        Transform _eyeNear, _eyeFar, _halo;
        Renderer _eyeNearR, _eyeFarR, _haloR;

        static readonly int PSpeed = Animator.StringToHash("Speed");
        static readonly int PVertical = Animator.StringToHash("VerticalSpeed");
        static readonly int PGrounded = Animator.StringToHash("Grounded");
        static readonly int PDash = Animator.StringToHash("Dash");
        static readonly int PCast = Animator.StringToHash("Cast");
        static readonly int PShield = Animator.StringToHash("Shield");
        static readonly int PHurt = Animator.StringToHash("Hurt");
        static readonly int PDead = Animator.StringToHash("Dead");
        static readonly int PSing = Animator.StringToHash("Sing");
        static readonly int PLocoRate = Animator.StringToHash("LocoRate");

        /// <summary>Instantiates the hero prefab under <paramref name="parent"/>. Returns null if anything is missing.</summary>
        public static UmbraHero Create(GameObject prefab, Transform parent, UmbraRig rig, UmbraController ctl, Material body, Material eye, Material halo)
        {
            if (prefab == null || ctl == null) return null;
            var go = Instantiate(prefab, parent, false);
            go.name = "UmbraModel";
            var anim = go.GetComponent<Animator>();
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (anim == null || smr == null || smr.sharedMesh == null || anim.avatar == null || !anim.avatar.isValid || !anim.isHuman
                || anim.runtimeAnimatorController == null || anim.GetBoneTransform(HumanBodyBones.Head) == null)
            {
                Debug.LogError("[LumenWave] Hero model incomplete (Resources/Hero/UmbraModel)");
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                return null;
            }
            var hero = go.AddComponent<UmbraHero>();
            hero.Setup(rig, ctl, anim, smr, body, eye, halo);
            return hero;
        }

        void Setup(UmbraRig rig, UmbraController ctl, Animator anim, SkinnedMeshRenderer smr, Material body, Material eye, Material halo)
        {
            _rig = rig; _ctl = ctl; _anim = anim; Body = smr;
            _health = ctl.GetComponent<PlayerHealth>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Player;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Height: bind-pose mesh bounds (head top to soles) → match the collider, soles on its bottom.
            var t0 = transform;
            t0.localPosition = Vector3.zero; t0.localRotation = Quaternion.identity; t0.localScale = Vector3.one;
            var b = smr.sharedMesh.bounds;
            float meshScale = smr.transform.lossyScale.y / Mathf.Max(1e-5f, t0.lossyScale.y);
            float scale = ctl.height * HeightFactor / Mathf.Max(0.01f, b.size.y * meshScale);
            t0.localScale = new Vector3(WidthFactor, 1f, WidthFactor) * scale;
            t0.localPosition = new Vector3(0f, -b.min.y * meshScale * scale, 0f);

            var mats = new Material[Mathf.Max(1, smr.sharedMesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = body;
            smr.sharedMaterials = mats;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smr.receiveShadows = false;

            NeckBone = anim.GetBoneTransform(HumanBodyBones.Neck) ?? anim.GetBoneTransform(HumanBodyBones.Head);
            HeadBone = anim.GetBoneTransform(HumanBodyBones.Head);

            _yaw = YawFor(ctl.Facing);
            t0.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            anim.Rebind();
            anim.Update(0f);
            BuildEyes(eye, halo);
            _nextBlink = Time.time + Random.Range(1.5f, 4f);
        }

        static float YawFor(int facing) => facing > 0 ? 90f + ThreeQuarterTurn : -90f - ThreeQuarterTurn;

        /// <summary>Eyes: small additive glow quads parented to the Head bone (placed from the idle pose).</summary>
        void BuildEyes(Material eye, Material halo)
        {
            var head = HeadBone;
            float H = Height;
            var fwd = transform.forward; var right = transform.right;
            var basePos = head.position + Vector3.up * (0.055f * H) + fwd * (0.05f * H);
            _eyeNear = GlowQuad("EyeNear", eye, head, basePos - right * (0.017f * H), out _eyeNearR);
            _eyeFar = GlowQuad("EyeFar", eye, head, basePos + right * (0.017f * H), out _eyeFarR);
            _halo = GlowQuad("EyeHalo", halo, head, basePos, out _haloR);
        }

        static Transform GlowQuad(string name, Material m, Transform head, Vector3 worldPos, out Renderer r)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.layer = Layers.Player;
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.transform.SetParent(head, true);
            go.transform.position = worldPos;
            r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>Echo / Pulse: upper-body cast clip.</summary>
        public void OnCast() { if (_anim != null) _anim.SetTrigger(PCast); }

        /// <summary>Damage: upper-body hit-reaction clip (throttled so continuous light burn doesn't retrigger it).</summary>
        public void OnHurt()
        {
            if (_anim == null || Time.time < _hurtCooldown) return;
            _hurtCooldown = Time.time + 0.45f;
            _anim.SetTrigger(PHurt);
        }

        void Update()
        {
            if (_ctl == null || _anim == null) return;
            bool dead = _health != null && _health.IsDead;
            var v = _ctl.Velocity;
            float speedN = Mathf.Abs(v.x) / Mathf.Max(0.01f, _ctl.maxSpeed);

            _anim.SetFloat(PSpeed, speedN, 0.05f, Time.deltaTime);
            _anim.SetFloat(PVertical, v.y);
            _anim.SetBool(PGrounded, _ctl.Grounded);
            _anim.SetBool(PDash, _ctl.IsDashing);
            _anim.SetBool(PShield, !dead && _ctl.Current.shieldHeld);
            _anim.SetBool(PDead, dead);
            _anim.SetBool(PSing, !dead && _rig.Singing > 0.5f);
            // Clips are authored for ~3–4 m/s; Umbra runs at 7 m/s, so the loop plays a bit faster.
            _anim.SetFloat(PLocoRate, Mathf.Lerp(1f, 1.45f, Mathf.Clamp01(speedN)));

            if (!dead && _wasDead) { _anim.Play("Idle", 0, 0f); _anim.Play("Empty", 1, 0f); _dissolve = 1f; }
            _wasDead = dead;
        }

        /// <summary>Called by UmbraRig.LateUpdate: root facing, whole-body squash, dissolve, eyes.</summary>
        public void LateTick(float dt, Transform visual, Vector3 squash, Material bodyMat)
        {
            if (_ctl == null) return;
            bool dead = _health != null && _health.IsDead;

            // Facing: quick turn of the whole model through the camera-facing view.
            _yaw = Mathf.MoveTowardsAngle(_yaw, YawFor(_ctl.Facing), dt * 1100f);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(squash.x, squash.y, 1f);

            // Dissolve: death burns away into motes; respawn materialises.
            _dissolve = dead ? Mathf.MoveTowards(_dissolve, 1f, dt / 0.85f) : Mathf.MoveTowards(_dissolve, 0f, dt / 0.5f);
            if (bodyMat != null)
            {
                bodyMat.SetFloat("_Dissolve", dead ? Mathf.Clamp01((_dissolve - 0.2f) / 0.8f) : _dissolve);
                bodyMat.SetFloat("_DissolveMinY", _rig.transform.position.y);
                bodyMat.SetFloat("_DissolveHeight", Height);
            }
            Body.shadowCastingMode = _dissolve > 0.01f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            UpdateEyes(dt, dead);
        }

        void UpdateEyes(float dt, bool dead)
        {
            if (_eyeNear == null) return;
            var cam = Camera.main;
            var camRot = cam != null ? cam.transform.rotation : Quaternion.identity;
            float H = Height;
            if (Time.time >= _nextBlink) { _blinkTimer = 0.12f; _nextBlink = Time.time + Random.Range(2.2f, 5.5f); }
            if (_blinkTimer > 0f) { _blinkTimer -= dt; _blink = 0.12f; }
            else _blink = Mathf.MoveTowards(_blink, 1f, dt * 14f);
            float vis = dead ? 0f : 1f - Mathf.Clamp01(_dissolve * 2f);
            var eye = new Vector3(0.042f * H, Mathf.Max(0.002f, 0.02f * H * _blink), 1f) * vis;
            Billboard(_eyeNear, _eyeNearR, camRot, eye);
            Billboard(_eyeFar, _eyeFarR, camRot, eye * 0.8f);
            Billboard(_halo, _haloR, camRot, new Vector3(0.22f * H, 0.15f * H, 1f) * vis * Mathf.Lerp(0.6f, 1f, _blink));
        }

        /// <summary>Face the camera with a given world size (compensating the head bone's scale).</summary>
        static void Billboard(Transform tr, Renderer r, Quaternion rot, Vector3 worldSize)
        {
            tr.rotation = rot;
            var ps = tr.parent != null ? tr.parent.lossyScale : Vector3.one;
            float s = Mathf.Max(1e-4f, Mathf.Abs(ps.x));
            tr.localScale = worldSize / s;
            if (r != null) r.enabled = worldSize.x > 0.0005f;
        }

        /// <summary>Snapshot of the current skinned pose for dash afterimages (caller owns the mesh).</summary>
        public Mesh BakeSnapshot(out Matrix4x4 localToWorld)
        {
            var m = new Mesh { name = "UmbraGhostMesh" };
            Body.BakeMesh(m, false);
            var tr = Body.transform;
            // Depending on where the rig carries its scale, the unscaled bake is either world-sized already or
            // in renderer units: pick the ghost scale that reproduces the live renderer's height.
            float baked = m.bounds.size.y, live = Body.bounds.size.y;
            var lossy = tr.lossyScale;
            bool needScale = Mathf.Abs(baked * lossy.y - live) < Mathf.Abs(baked - live);
            localToWorld = Matrix4x4.TRS(tr.position, tr.rotation, needScale ? lossy : Vector3.one);
            return m;
        }
    }
}
