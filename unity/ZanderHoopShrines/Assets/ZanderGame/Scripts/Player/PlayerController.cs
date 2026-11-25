using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Zander's movement: camera-relative running, sprinting, jumping (with coyote
    /// time), riding moving platforms, bounce pads, knock-backs and respawning.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }
        public const float Gravity = 22f;
        public const float Height = 1.45f;

        public float WalkSpeed = 5.2f;
        public float DashSpeed = 9.5f;

        // ---- dash + stamina wheel ----
        public const float DashDrainSeconds = 3.5f;        // full wheel of dashing
        public const float RechargeSeconds = 2.0f;         // empty -> full after letting go
        public const float ExhaustedRechargeSeconds = 4.5f;// ran out completely: slower
        public float Stamina { get; private set; } = 1f;   // 0..1
        public bool Exhausted { get; private set; }        // hit zero: slow + no dashing until full again
        public const float WindedSpeedFactor = 0.5f;      // half speed while out of breath
        public bool Dashing { get; private set; }
        public float StaminaShownUntil { get; private set; }
        float _lungeUntil;
        Vector3 _lungeVel;
        public float JumpHeight = 1.5f;
        public float KillY = -4f;

        public CharacterController CC { get; private set; }
        public PlayerBallHandler Balls { get; private set; }
        public PlayerAnimation Anim { get; private set; }
        public ThirdPersonCamera Cam;
        public Transform Visual;

        public bool Grounded { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public Vector3 Forward => Visual != null ? Visual.forward : transform.forward;
        public bool IsHanging => Time.time < _hangUntil || Scripted;

        /// <summary>True while a dunk moves Zander directly (leap to the rim, rim hang).</summary>
        public bool Scripted { get; private set; }
        public bool IsStunned => Time.time < _stunUntil;

        Vector3 _hVel;
        float _vVel;
        Vector3 _external;
        float _coyote;
        float _padCooldown;
        float _stunUntil;
        float _hangUntil;
        Vector3 _hangPos;
        Collider _ground;
        Quaternion _faceTarget = Quaternion.identity;
        Interactable _nearInteract;

        public string InteractPrompt { get; private set; }

        public static PlayerController Spawn(Vector3 pos, float yaw)
        {
            var go = new GameObject("Zander");
            go.transform.position = pos;
            go.layer = Build.IgnoreRaycastLayer;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.4f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0, 0.72f, 0);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            var pc = go.AddComponent<PlayerController>();
            pc.Visual = CharacterFactory.CreateZander(go.transform);
            pc.Visual.localRotation = Quaternion.Euler(0, yaw, 0);
            pc._faceTarget = pc.Visual.localRotation;
            Build.SetLayerRecursive(go, Build.IgnoreRaycastLayer);
            return pc;
        }

        void Awake()
        {
            Instance = this;
            CC = GetComponent<CharacterController>();
        }

        void Start()
        {
            Balls = gameObject.AddComponent<PlayerBallHandler>();
            Anim = gameObject.AddComponent<PlayerAnimation>();
            Anim.Init(this);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            bool frozen = gm != null && !gm.IsPlaying;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Scripted)
            {
                Grounded = false;
                HorizontalSpeed = 0f;
                InteractPrompt = null;
                return;
            }

            // ---- ride moving platforms ----
            if (_ground != null)
            {
                Vector3 carry = Vector3.zero;
                var sm = _ground.GetComponentInParent<SignalMover>();
                if (sm != null) carry = sm.LastDelta;
                var mp = _ground.GetComponentInParent<MovingPlatform>();
                if (mp != null) carry = mp.LastDelta;
                if (carry.sqrMagnitude > 0f) CC.Move(carry);
            }

            Grounded = CC.isGrounded;
            if (Grounded)
            {
                _coyote = 0.12f;
                if (_vVel < 0) _vVel = -2f;
            }
            else _coyote -= dt;

            // ---- bounce pads ----
            _padCooldown -= dt;
            if (Grounded && _ground != null && _padCooldown <= 0f)
            {
                var pad = _ground.GetComponentInParent<BouncePad>();
                if (pad != null && pad.Powered)
                {
                    _padCooldown = 0.5f;
                    pad.Launch(this);
                }
            }

            // ---- input ----
            Vector2 mv = frozen || IsStunned || IsHanging ? Vector2.zero : Controls.Move;
            float camYaw = Cam != null ? Cam.Yaw : 0f;
            Vector3 wish = Quaternion.Euler(0, camYaw, 0) * new Vector3(mv.x, 0, mv.y);
            bool charging = Balls != null && Balls.Charging;
            UpdateStamina(frozen, mv, charging, dt);
            float speed = Dashing ? DashSpeed : Exhausted ? WalkSpeed * WindedSpeedFactor : WalkSpeed;
            if (charging) speed *= 0.35f;

            if (!frozen && !IsStunned && Controls.JumpDown && _coyote > 0f && !IsHanging)
            {
                _vVel = Mathf.Sqrt(2f * Gravity * JumpHeight);
                _coyote = 0f;
                Anim?.OnJump();
            }

            // ---- interactables ----
            UpdateInteract(frozen);

            // ---- integrate ----
            if (IsHanging)
            {
                _hVel = Vector3.zero;
                _vVel = 0;
                var delta = _hangPos - transform.position;
                CC.Move(delta);
            }
            else
            {
                float accel = Grounded ? 45f : 20f;
                _hVel = Vector3.MoveTowards(_hVel, wish * speed, accel * dt);
                _vVel -= Gravity * dt;
                _external = Vector3.MoveTowards(_external, Vector3.zero, 12f * dt);
                _ground = null;
                var lunge = Time.time < _lungeUntil ? _lungeVel : Vector3.zero;
                var flags = CC.Move((_hVel + _external + lunge + Vector3.up * _vVel) * dt);
                if ((flags & CollisionFlags.Above) != 0 && _vVel > 0) _vVel = 0;
            }
            HorizontalSpeed = new Vector3(_hVel.x, 0, _hVel.z).magnitude;

            // ---- facing ----
            if (charging && Balls.AimPoint.HasValue)
            {
                var d = Balls.AimPoint.Value - transform.position;
                d.y = 0;
                if (d.sqrMagnitude > 0.01f) _faceTarget = Quaternion.LookRotation(d);
            }
            else if (charging && Cam != null)
                _faceTarget = Quaternion.Euler(0, Cam.Yaw, 0);
            else if (wish.sqrMagnitude > 0.01f)
                _faceTarget = Quaternion.LookRotation(wish);
            if (Visual != null)
                Visual.rotation = Quaternion.RotateTowards(Visual.rotation, _faceTarget, 900f * dt);

            if (transform.position.y < KillY && gm != null) gm.RespawnPlayer(true);
        }

        void UpdateStamina(bool frozen, Vector2 mv, bool charging, float dt)
        {
            bool held = !frozen && Controls.Sprint;
            bool moving = mv.sqrMagnitude > 0.04f;
            bool wasDashing = Dashing;
            Dashing = held && moving && !Exhausted && !charging && !IsStunned;
            if (Dashing)
            {
                Stamina -= dt / DashDrainSeconds;
                if (Stamina <= 0f)
                {
                    Stamina = 0f;
                    Exhausted = true;
                    Dashing = false;
                    GameAudio.Play("unclick", 0.8f, 0.7f);
                    Hud.Toast("Out of breath!", new Color(1f, 0.55f, 0.4f), 1.0f);
                }
                if (!wasDashing)
                {
                    Effects.Burst(transform.position + Vector3.up * 0.1f, new Color(0.9f, 0.85f, 0.75f), 12, 2.5f, 0.12f, 0.4f, false);
                    GameAudio.Play("throw", 0.5f, 0.7f);
                }
            }
            else if (!held || Exhausted)
            {
                // recharges as soon as the button is let go (slower if it ran out completely)
                Stamina += dt / (Exhausted ? ExhaustedRechargeSeconds : RechargeSeconds);
                if (Stamina >= 1f)
                {
                    Stamina = 1f;
                    if (Exhausted) GameAudio.Play("pickup", 0.4f, 1.2f);
                    Exhausted = false;
                }
            }
            if (Stamina < 1f || Dashing) StaminaShownUntil = Time.time + 1.0f;
        }

        /// <summary>A short burst forward (steal swipe) without stunning.</summary>
        public void Lunge(Vector3 dir, float speed, float seconds)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) return;
            _lungeVel = dir.normalized * speed;
            _lungeUntil = Time.time + seconds;
            _faceTarget = Quaternion.LookRotation(dir);
        }

        void UpdateInteract(bool frozen)
        {
            _nearInteract = null;
            InteractPrompt = null;
            float best = float.MaxValue;
            foreach (var it in Interactable.All)
            {
                if (it == null || string.IsNullOrEmpty(it.Prompt)) continue;
                var d = it.Point - transform.position;
                d.y = 0;
                float m = d.magnitude;
                if (m < it.Range && m < best)
                {
                    best = m;
                    _nearInteract = it;
                }
            }
            if (_nearInteract != null) InteractPrompt = _nearInteract.Prompt;
            else if (Balls != null && Balls.CanStealFromBoss) InteractPrompt = "Press {USE} to STEAL the ball!";
            else if (Balls != null && Balls.PlateBallNearby != null) InteractPrompt = "Press {USE} to pick up the ball";

            if (frozen || !Controls.InteractDown) return;
            if (_nearInteract != null) _nearInteract.Interact(this);
            else if (Balls != null && Balls.TrySteal()) { }
            else Balls?.GrabPlateBall();
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f) _ground = hit.collider;
            var rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic)
            {
                var push = hit.moveDirection;
                push.y = 0;
                rb.AddForce(push * 2.5f, ForceMode.VelocityChange);
            }
        }

        // ---------------------------------------------------------------- API --
        public void Launch(float upVelocity)
        {
            _vVel = upVelocity;
            _coyote = 0f;
            Anim?.OnJump();
        }

        public void Knockback(Vector3 dir, float force)
        {
            if (IsStunned || Scripted) return;
            dir.y = 0;
            _external = dir.normalized * force;
            _vVel = 6f;
            _stunUntil = Time.time + 0.8f;
            Balls?.DropBall(true);
            Anim?.OnHit();
            GameAudio.Play("bounce", 1f, 0.6f);
            Cam?.Shake(0.4f);
            Rumble.Pulse(1f, 0.8f, 0.4f);
        }

        /// <summary>Take direct control of Zander's position (no collisions, no gravity).</summary>
        public void BeginScripted()
        {
            Scripted = true;
            CC.enabled = false;
            _hVel = Vector3.zero;
            _vVel = 0f;
            _external = Vector3.zero;
        }

        public void ScriptedPose(Vector3 pos, Vector3 faceDir)
        {
            transform.position = pos;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.0001f && Visual != null)
            {
                _faceTarget = Quaternion.LookRotation(faceDir);
                Visual.rotation = Quaternion.RotateTowards(Visual.rotation, _faceTarget, 1080f * Time.deltaTime);
            }
        }

        /// <summary>Give control back (e.g. letting go of the rim) with a little push.</summary>
        public void EndScripted(Vector3 push)
        {
            if (!Scripted) return;
            Scripted = false;
            CC.enabled = true;
            _external = push;
            _vVel = 0f;
            _padCooldown = 0.4f;
        }

        public void Hang(Vector3 at, float seconds)
        {
            _hangPos = at;
            _hangUntil = Time.time + seconds;
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            Scripted = false;
            CC.enabled = false;
            transform.position = pos;
            CC.enabled = true;
            _hVel = Vector3.zero;
            _vVel = 0;
            _external = Vector3.zero;
            _hangUntil = 0;
            if (Visual != null)
            {
                Visual.rotation = Quaternion.Euler(0, yaw, 0);
                _faceTarget = Visual.rotation;
            }
            Cam?.SnapBehind(yaw);
        }
    }
}
