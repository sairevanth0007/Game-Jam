using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Skinned hero body for Umbra (CC0 Quaternius "Animated Human", Humanoid) driven by
    /// Resources/Hero/UmbraAnimator.controller. Lives on the model GameObject (next to the Animator) so it
    /// receives OnAnimatorIK. Owned and ticked by <see cref="UmbraRig"/>:
    ///  - Update: picks the Animator state (Idle, Locomotion walk→run, Jump, Fall, Land, Dash, Death, Respawn)
    ///    and drives parameters (Speed, LocoRate, AirTime, CastTime, cast layer weight).
    ///  - OnAnimatorIK: foot planting on Ground/Bridge when standing, hand goals for shield brace and singing,
    ///    head look-at.
    ///  - LateTick (after the Animator): facing yaw with a 3/4 turn toward the camera, lean into velocity,
    ///    breathing, hurt flinch, dash stretch, glowing eyes with blinks, dissolve on death/respawn.
    /// </summary>
    public class UmbraHero : MonoBehaviour
    {
        /// <summary>Rendered height relative to the controller's collider height.</summary>
        public const float HeightFactor = 1.06f;
        /// <summary>Extra girth (model X/Z) so the silhouette reads bulky like a Shadow Fight fighter.</summary>
        public const float WidthFactor = 1.14f;
        const float ThreeQuarterTurn = 22f;

        public SkinnedMeshRenderer Body { get; private set; }
        public Transform NeckBone { get; private set; }
        public Transform HeadBone { get; private set; }
        public Animator Animator => _anim;

        UmbraRig _rig;
        UmbraController _ctl;
        PlayerHealth _health;
        Animator _anim;
        Transform _hips, _spine, _chest, _lFoot, _rFoot;
        float _modelScale, _yaw, _lean, _spineLean, _blinkTimer, _blink = 1f, _nextBlink;
        float _castTimer, _castWeight, _fallTime, _landTime, _hurtTimer, _dissolve, _dissolveTarget;
        int _state = -1;
        bool _wasDead;
        float _shieldW, _singW, _guardW;
        Transform _eyeNear, _eyeFar, _halo;
        Vector3 _eyeLocalNear, _eyeLocalFar, _haloLocal;
        Material _eyeMat, _haloMat;
        int _groundMask;

        static readonly int HSpeed = Animator.StringToHash("Speed");
        static readonly int HLocoRate = Animator.StringToHash("LocoRate");
        static readonly int HAirTime = Animator.StringToHash("AirTime");
        static readonly int HCastTime = Animator.StringToHash("CastTime");

        const int SIdle = 0, SLoco = 1, SJump = 2, SFall = 3, SLand = 4, SDash = 5, SDeath = 6, SRespawn = 7;
        static readonly string[] StateNames = { "Idle", "Locomotion", "Jump", "Fall", "Land", "Dash", "Death", "Respawn" };
        static readonly float[] Fade = { 0.15f, 0.12f, 0.06f, 0.18f, 0.05f, 0.05f, 0.08f, 0f };

        public float CastDuration = 0.45f;
        public float Dissolve => _dissolve;
        public float Height => _ctl != null ? _ctl.height * HeightFactor : 1.8f;

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
                Debug.LogWarning("[LumenWave] Hero model incomplete, using the primitive rig");
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
            _groundMask = Layers.Mask(Layers.Ground, Layers.Bridge, Layers.Crystal, Layers.Default);
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Player;

            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.updateMode = AnimatorUpdateMode.Normal;

            // Height: bind-pose mesh bounds (head top to soles) in model-root units → match the collider.
            var t0 = transform;
            t0.localPosition = Vector3.zero; t0.localRotation = Quaternion.identity; t0.localScale = Vector3.one;
            var b = smr.sharedMesh.bounds;
            float meshScale = smr.transform.lossyScale.y / Mathf.Max(1e-5f, t0.lossyScale.y);
            float meshH = b.size.y * meshScale;
            _modelScale = ctl.height * HeightFactor / Mathf.Max(0.01f, meshH);
            t0.localScale = new Vector3(WidthFactor, 1f, WidthFactor) * _modelScale;
            t0.localPosition = new Vector3(0f, -b.min.y * meshScale * _modelScale, 0f);

            smr.sharedMaterials = new[] { body };
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smr.receiveShadows = false;

            _hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            _spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            _chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
            NeckBone = anim.GetBoneTransform(HumanBodyBones.Neck) ?? anim.GetBoneTransform(HumanBodyBones.Head);
            HeadBone = anim.GetBoneTransform(HumanBodyBones.Head);
            _lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);

            _yaw = YawFor(ctl.Facing);
            t0.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            anim.Rebind();
            anim.Update(0f);
            BuildEyes(eye, halo);
            _nextBlink = Time.time + Random.Range(1.5f, 4f);
        }

        static float YawFor(int facing) => facing > 0 ? 90f + ThreeQuarterTurn : -90f - ThreeQuarterTurn;

        void BuildEyes(Material eye, Material halo)
        {
            _eyeMat = eye; _haloMat = halo;
            var head = HeadBone;
            float H = Height;
            // Eye positions measured in the current (idle) pose, stored in head-bone space so they follow the head.
            var fwd = transform.forward; var right = transform.right;
            var basePos = head.position + Vector3.up * (0.05f * H) + fwd * (0.045f * H);
            _eyeLocalNear = head.InverseTransformPoint(basePos - right * (0.017f * H));
            _eyeLocalFar = head.InverseTransformPoint(basePos + right * (0.017f * H));
            _haloLocal = head.InverseTransformPoint(basePos);
            _eyeNear = GlowQuad("EyeNear", eye);
            _eyeFar = GlowQuad("EyeFar", eye);
            _halo = GlowQuad("EyeHalo", halo);
        }

        Transform GlowQuad(string name, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.layer = Layers.Player;
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.transform.SetParent(_rig.transform, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        public void OnCast() { _castTimer = CastDuration; }
        public void OnHurt() { _hurtTimer = 0.3f; }

        int PickState(bool dead)
        {
            var v = _ctl.Velocity;
            if (dead) return SDeath;
            if (_ctl.IsDashing) return SDash;
            if (!_ctl.Grounded) return v.y > 0.5f ? SJump : SFall;
            bool moving = Mathf.Abs(v.x) > 0.4f;
            if (_ctl.TimeSinceLanded < 0.22f && _ctl.LastLandingSpeed > 7f && Mathf.Abs(v.x) < 3f) return SLand;
            return moving ? SLoco : SIdle;
        }

        void Update()
        {
            if (_ctl == null || _anim == null) return;
            float dt = Time.deltaTime;
            bool dead = _health != null && _health.IsDead;
            var v = _ctl.Velocity;

            if (!dead && _wasDead)
            {
                // Respawn: snap to idle and materialise (reverse dissolve).
                _anim.Play("Idle", 0, 0f);
                _state = SIdle;
                _dissolve = 1f; _dissolveTarget = 0f;
            }
            _wasDead = dead;

            int s = PickState(dead);
            if (s != _state)
            {
                if (s == SLand) _landTime = 0f;
                if (s == SFall && _state != SJump) _fallTime = 0f;
                if (s == SFall && _state == SJump) _fallTime = 0f;
                _anim.CrossFadeInFixedTime(StateNames[s], _state < 0 ? 0f : Fade[s], 0);
                _state = s;
            }

            float speedN = Mathf.Abs(v.x) / Mathf.Max(0.01f, _ctl.maxSpeed);
            _anim.SetFloat(HSpeed, speedN, 0.06f, dt);
            _anim.SetFloat(HLocoRate, Mathf.Lerp(0.9f, 1.75f, Mathf.Clamp01(speedN)));

            float air = 0.36f;
            switch (s)
            {
                case SJump:
                {
                    float v0 = Mathf.Sqrt(2f * _ctl.gravity * _ctl.jumpHeight);
                    float k = 1f - Mathf.Clamp01(v.y / Mathf.Max(0.1f, v0));
                    air = Mathf.Lerp(0.13f, 0.34f, k);
                    break;
                }
                case SFall:
                    _fallTime += dt;
                    air = Mathf.Lerp(0.38f, 0.56f, Mathf.Clamp01(-v.y / 14f)) + Mathf.Sin(Time.time * 3.1f) * 0.012f;
                    break;
                case SLand:
                    _landTime += dt;
                    air = Mathf.Lerp(0.70f, 0.95f, Mathf.Clamp01(_landTime / 0.22f));
                    break;
            }
            _anim.SetFloat(HAirTime, air);

            // Cast (Echo / Pulse): upper-body punch on the override layer.
            if (_castTimer > 0f && !dead)
            {
                _castTimer -= dt;
                float k = 1f - Mathf.Clamp01(_castTimer / CastDuration);
                _anim.SetFloat(HCastTime, Mathf.Lerp(0.22f, 0.62f, 1f - (1f - k) * (1f - k)));
                _castWeight = Mathf.MoveTowards(_castWeight, k < 0.75f ? 1f : 0f, dt * (k < 0.75f ? 14f : 8f));
            }
            else _castWeight = Mathf.MoveTowards(_castWeight, 0f, dt * 8f);
            if (_anim.layerCount > 1) _anim.SetLayerWeight(1, _castWeight);

            var cmd = _ctl.Current;
            _shieldW = Mathf.MoveTowards(_shieldW, (!dead && cmd.shieldHeld) ? 1f : 0f, dt * 9f);
            _singW = dead ? 0f : Mathf.Clamp01(_rig.Singing);
            bool guarding = !dead && _state == SIdle && _ctl.Grounded && Mathf.Abs(v.x) < 0.4f;
            _guardW = Mathf.MoveTowards(_guardW, guarding ? 1f : 0f, dt * (guarding ? 3f : 10f));
        }

        void OnAnimatorIK(int layer)
        {
            if (_ctl == null || _anim == null) return;
            bool dead = _health != null && _health.IsDead;
            float H = Height;
            int f = _ctl.Facing;
            var root = _rig.transform.position;

            // Guard stance (idle): crouch a little, narrow the clip's wide front/back stride, fists up.
            float guard = _guardW * (1f - _shieldW) * (1f - _singW) * (1f - _castWeight);
            if (layer == 0 && guard > 0.001f)
            {
                float breath = Mathf.Sin(Time.time * 2.2f);
                _anim.bodyPosition += Vector3.down * ((0.045f + 0.006f * breath) * H * guard);
            }

            // Feet: plant on the ground when standing still (subtle, stable).
            float footW = (!dead && _ctl.Grounded && (_state == SIdle || _state == SLand) && Mathf.Abs(_ctl.Velocity.x) < 1f) ? 1f : 0f;
            float narrow = Mathf.Lerp(1f, 0.5f, guard);
            PlantFoot(AvatarIKGoal.LeftFoot, footW, root, H, narrow, f);
            PlantFoot(AvatarIKGoal.RightFoot, footW, root, H, narrow, f);

            // Hands: guard (fists in front of the chest), shield brace (forearms up in front of the face) and
            // singing (arms raised in a V). Weighted blend of the active poses.
            float shield = _shieldW * (1f - _singW);
            float sing = _singW;
            float handW = Mathf.Max(Mathf.Max(shield, sing), guard * 0.8f);
            // Near hand = the one closer to the camera: right hand when facing +X (model turned 3/4 to camera).
            var nearGoal = f > 0 ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            var farGoal = f > 0 ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            var nearHint = f > 0 ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow;
            var farHint = f > 0 ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
            if (handW > 0.001f)
            {
                float br = Mathf.Sin(Time.time * 2.6f);
                float gb = Mathf.Sin(Time.time * 2.2f) * 0.008f;
                var shieldNear = root + new Vector3(f * 0.30f * H, 0.80f * H, -0.10f * H);
                var shieldFar = root + new Vector3(f * 0.26f * H, 0.90f * H, 0.08f * H);
                var singNear = root + new Vector3(f * 0.42f * H, (1.12f + 0.03f * br) * H, -0.2f * H);
                var singFar = root + new Vector3(-f * 0.36f * H, (1.08f - 0.03f * br) * H, 0.22f * H);
                var guardNear = root + new Vector3(f * 0.24f * H, (0.66f + gb) * H, -0.13f * H);
                var guardFar = root + new Vector3(f * 0.15f * H, (0.73f + gb) * H, 0.1f * H);
                float g = guard * 0.8f, sum = Mathf.Max(0.001f, shield + sing + g);
                var pn = (shieldNear * shield + singNear * sing + guardNear * g) / sum;
                var pf = (shieldFar * shield + singFar * sing + guardFar * g) / sum;
                SetHand(nearGoal, pn, handW);
                SetHand(farGoal, pf, handW);
                // Elbows: down and slightly forward for guard, out in front for the shield brace.
                float hintW = Mathf.Max(shield * 0.8f, g * 0.7f);
                var hn = Vector3.Lerp(root + new Vector3(f * 0.08f * H, 0.5f * H, -0.2f * H), root + new Vector3(f * 0.30f * H, 0.6f * H, -0.2f * H), shield / Mathf.Max(0.001f, shield + g));
                var hf = Vector3.Lerp(root + new Vector3(f * 0.02f * H, 0.52f * H, 0.2f * H), root + new Vector3(f * 0.24f * H, 0.62f * H, 0.2f * H), shield / Mathf.Max(0.001f, shield + g));
                _anim.SetIKHintPositionWeight(nearHint, hintW);
                _anim.SetIKHintPositionWeight(farHint, hintW);
                _anim.SetIKHintPosition(nearHint, hn);
                _anim.SetIKHintPosition(farHint, hf);
            }
            else
            {
                SetHand(AvatarIKGoal.LeftHand, Vector3.zero, 0f);
                SetHand(AvatarIKGoal.RightHand, Vector3.zero, 0f);
                _anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                _anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            }

            // Head: singing looks up and back (chest opens with it); otherwise a slight look along the motion.
            if (!dead)
            {
                var v = _ctl.Velocity;
                var ahead = root + new Vector3(f * 3f * H, 0.9f * H + Mathf.Clamp(v.y * 0.08f, -0.6f, 0.6f) * H, -1.2f * H);
                var up = root + new Vector3(f * 1.3f * H, 2.3f * H, -0.5f * H);
                _anim.SetLookAtWeight(Mathf.Lerp(0.35f, 1f, sing), Mathf.Lerp(0.05f, 0.2f, sing), Mathf.Lerp(0.5f, 0.8f, sing), 0f, 0.5f);
                _anim.SetLookAtPosition(Vector3.Lerp(ahead, up, sing));
            }
            else _anim.SetLookAtWeight(0f);
        }

        void SetHand(AvatarIKGoal g, Vector3 p, float w)
        {
            _anim.SetIKPositionWeight(g, w);
            _anim.SetIKRotationWeight(g, 0f);
            if (w > 0f) _anim.SetIKPosition(g, p);
        }

        void PlantFoot(AvatarIKGoal g, float w, Vector3 root, float H, float narrow, int f)
        {
            if (w <= 0f) { _anim.SetIKPositionWeight(g, 0f); _anim.SetIKRotationWeight(g, 0f); return; }
            var p = _anim.GetIKPosition(g);
            // Narrow the stride toward a guard stance (slightly forward of centre so the weight sits over the feet).
            p.x = root.x + f * 0.02f * H * (1f - narrow) + (p.x - root.x) * narrow;
            const float AnkleHeight = 0.036f;                // ankle above the soles in the standing pose (fraction of H)
            float ankle = AnkleHeight * H;
            var from = new Vector3(p.x, root.y + 0.35f * H, p.z);
            if (Physics.Raycast(from, Vector3.down, out var hit, 0.6f * H, _groundMask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.point.y - root.y) < 0.18f * H && hit.normal.y > 0.6f)
            {
                _anim.SetIKPositionWeight(g, w);
                _anim.SetIKPosition(g, new Vector3(p.x, hit.point.y + ankle, p.z));
                _anim.SetIKRotationWeight(g, w * 0.5f);
                _anim.SetIKRotation(g, Quaternion.FromToRotation(Vector3.up, hit.normal) * _anim.GetIKRotation(g));
            }
            else
            {
                // No floor under this foot (ledge edge): keep the narrowed stance at body level.
                _anim.SetIKPositionWeight(g, w);
                _anim.SetIKPosition(g, new Vector3(p.x, root.y + ankle, p.z));
                _anim.SetIKRotationWeight(g, 0f);
            }
        }

        /// <summary>Called by UmbraRig.LateUpdate after the Animator has posed the skeleton.</summary>
        public void LateTick(float dt, Transform visual, Vector3 squash, Material bodyMat)
        {
            if (_ctl == null) return;
            bool dead = _health != null && _health.IsDead;
            var v = _ctl.Velocity;
            int f = _ctl.Facing;
            float t = Time.time;
            float speedN = Mathf.Clamp01(Mathf.Abs(v.x) / Mathf.Max(0.01f, _ctl.maxSpeed));

            // Facing: quick turn through the camera-facing view.
            _yaw = Mathf.MoveTowardsAngle(_yaw, YawFor(f), dt * 1100f);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);

            // Whole-body lean into velocity (pivot at the feet).
            float leanT = 0f;
            if (!dead)
            {
                if (_state == SDash) leanT = -f * 24f;
                else if (_ctl.Grounded) leanT = -v.x / Mathf.Max(0.01f, _ctl.maxSpeed) * 6f;
                else leanT = -v.x / Mathf.Max(0.01f, _ctl.maxSpeed) * 4f;
            }
            _lean = Mathf.Lerp(_lean, leanT, 1f - Mathf.Exp(-dt * 14f));
            visual.localRotation = Quaternion.Euler(0f, 0f, _lean);
            visual.localScale = new Vector3(squash.x, squash.y, 1f);

            // Upper-body overlays (rotations about world Z = the body's sagittal axis in side view).
            float spineT = 0f;
            if (!dead)
            {
                if (_state == SLoco) spineT = -f * 10f * speedN;
                if (_state == SDash) spineT = -f * 16f;
                if (_hurtTimer > 0f) spineT += f * 22f * Mathf.Sin(Mathf.Clamp01(_hurtTimer / 0.3f) * Mathf.PI);
                spineT += f * 6f * _singW;
                spineT += f * 4f * _shieldW;
            }
            _spineLean = Mathf.Lerp(_spineLean, spineT, 1f - Mathf.Exp(-dt * 18f));
            if (_spine != null && Mathf.Abs(_spineLean) > 0.01f) _spine.rotation = Quaternion.AngleAxis(_spineLean * 0.5f, Vector3.forward) * _spine.rotation;
            if (_chest != null)
            {
                float breathe = (!dead && _state == SIdle) ? Mathf.Sin(t * 2.2f) * 1.6f : 0f;
                _chest.rotation = Quaternion.AngleAxis(_spineLean * 0.5f + f * breathe, Vector3.forward) * _chest.rotation;
            }
            _hurtTimer -= dt;

            // Dissolve: death burns away into motes; respawn materialises.
            if (dead)
            {
                _dissolveTarget = 1f;
                _dissolve = Mathf.MoveTowards(_dissolve, _dissolveTarget, dt / 0.85f);
            }
            else _dissolve = Mathf.MoveTowards(_dissolve, 0f, dt / 0.5f);
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
            if (_eyeNear == null || HeadBone == null) return;
            var cam = Camera.main;
            var camRot = cam != null ? cam.transform.rotation : Quaternion.identity;
            var toCam = cam != null ? -cam.transform.forward : Vector3.back;
            float H = Height;

            if (Time.time >= _nextBlink) { _blinkTimer = 0.12f; _nextBlink = Time.time + Random.Range(2.2f, 5.5f); }
            if (_blinkTimer > 0f) { _blinkTimer -= dt; _blink = 0.12f; }
            else _blink = Mathf.MoveTowards(_blink, 1f, dt * 14f);

            float vis = dead ? 0f : 1f - Mathf.Clamp01(_dissolve * 2f);
            float eyeW = 0.042f * H, eyeH = 0.02f * H * _blink;
            var scaleEye = new Vector3(eyeW, Mathf.Max(0.002f, eyeH), 1f) * vis;
            Place(_eyeNear, HeadBone.TransformPoint(_eyeLocalNear) + toCam * 0.03f * H, camRot, scaleEye);
            Place(_eyeFar, HeadBone.TransformPoint(_eyeLocalFar) + toCam * 0.03f * H, camRot, scaleEye * 0.8f);
            Place(_halo, HeadBone.TransformPoint(_haloLocal) + toCam * 0.07f * H, camRot, new Vector3(0.22f * H, 0.15f * H, 1f) * vis * Mathf.Lerp(0.6f, 1f, _blink));
        }

        static void Place(Transform tr, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            tr.position = pos;
            tr.rotation = rot;
            tr.localScale = scale;
            var r = tr.GetComponent<Renderer>();
            if (r != null) r.enabled = scale.x > 0.0005f;
        }

        /// <summary>Snapshot of the current skinned pose for dash afterimages (caller owns the mesh).</summary>
        public Mesh BakeSnapshot(out Matrix4x4 localToWorld)
        {
            var m = new Mesh { name = "UmbraGhostMesh" };
            // useScale=false: vertices come out at world size relative to the renderer (the bones already carry
            // the model's 69x armature scale); useScale=true would divide it back out. Ghost uses unit scale.
            Body.BakeMesh(m, false);
            var tr = Body.transform;
            localToWorld = Matrix4x4.TRS(tr.position, tr.rotation, Vector3.one);
            return m;
        }

        void OnDestroy()
        {
            if (_eyeNear != null) Destroy(_eyeNear.gameObject);
            if (_eyeFar != null) Destroy(_eyeFar.gameObject);
            if (_halo != null) Destroy(_halo.gameObject);
        }
    }
}
