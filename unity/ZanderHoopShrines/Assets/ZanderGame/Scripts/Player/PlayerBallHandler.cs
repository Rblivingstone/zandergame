using UnityEngine;

namespace ZanderGame
{
    public enum ShotQuality { Perfect, Close, Short, Long, Free }

    /// <summary>
    /// Zander's basketball skills: auto pick-up, dribbling, a timing-based shot
    /// meter with kid-friendly aim assist, dunks, and placing balls on plates.
    /// </summary>
    public class PlayerBallHandler : MonoBehaviour
    {
        public static bool SuperShots;              // Tab: every shot is perfect
        public const float SweetMin = 0.60f, SweetMax = 0.88f;
        public const float OkMin = 0.42f, OkMax = 0.97f;

        public Basketball Ball { get; private set; }
        public bool HasBall => Ball != null;
        public bool Charging { get; private set; }
        public float Power { get; private set; }
        public Component AimTarget { get; private set; }
        public Vector3? AimPoint { get; private set; }
        public Hoop DunkHoop { get; private set; }
        public Basketball PlateBallNearby { get; private set; }
        public string AimName
        {
            get
            {
                if (AimTarget is Hoop h) return h.DisplayName;
                if (AimTarget is ReboundBoard b) return b.DisplayName;
                return null;
            }
        }

        PlayerController _pc;
        float _pickupCooldown;
        float _chargeT;
        float _dribbleT;
        bool _wasLow;

        void Awake() => _pc = GetComponent<PlayerController>();

        Vector3 ReleasePoint => transform.position + Vector3.up * 1.75f + _pc.Forward * 0.25f;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying)
            {
                Charging = false;
                return;
            }

            if (Ball == null)
            {
                Charging = false;
                AimTarget = null;
                AimPoint = null;
                DunkHoop = null;
                if (Time.time > _pickupCooldown && !_pc.IsStunned) TryAutoPickup();
                FindPlateBall();
                return;
            }
            PlateBallNearby = null;

            UpdateAim();
            UpdateDunk();

            if (Controls.ShootDown && !_pc.IsStunned)
            {
                if (DunkHoop != null)
                {
                    StartCoroutine(DoDunk(DunkHoop));
                    return;
                }
                Charging = true;
                _chargeT = 0f;
            }
            if (Charging)
            {
                _chargeT += Time.deltaTime;
                Power = Mathf.PingPong(_chargeT * 1.05f, 1f);
                if (!Controls.ShootHeld) Shoot();
            }
            if (Ball != null && Controls.DropDown && !Charging) DropOrPlace();
        }

        void LateUpdate()
        {
            if (Ball == null) return;
            var p = transform.position;
            var fwd = _pc.Forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            Vector3 pos;
            if (Charging || _pc.IsHanging)
            {
                pos = p + Vector3.up * 1.78f + fwd * 0.12f;
                _dribbleT = 0;
            }
            else if (!_pc.Grounded)
            {
                pos = p + Vector3.up * 0.62f + fwd * 0.34f;
                _dribbleT = 0;
            }
            else
            {
                float period = _pc.HorizontalSpeed > 6f ? 0.36f : 0.5f;
                _dribbleT += Time.deltaTime;
                float handY = p.y + 0.46f;
                float floorY = p.y + Basketball.Radius;
                float k = Mathf.Abs(Mathf.Cos(Mathf.PI * _dribbleT / period));
                bool low = k < 0.15f;
                if (low && !_wasLow) GameAudio.Play("bounce", 0.35f, Random.Range(0.95f, 1.05f));
                _wasLow = low;
                pos = p + right * 0.26f + fwd * 0.34f;
                pos.y = floorY + (handY - floorY) * k;
            }
            Ball.transform.position = pos;
            Ball.transform.Rotate(right, 360f * Time.deltaTime, Space.World);
        }

        // ------------------------------------------------------------ pickup --
        void TryAutoPickup()
        {
            var p = transform.position;
            foreach (var b in Basketball.All)
            {
                if (b == null || b.IsHeld || !b.PlayerCanGrab) continue;
                var d = b.transform.position - p;
                float flat = new Vector2(d.x, d.z).magnitude;
                if (flat > 0.95f || d.y < -0.4f || d.y > 2.1f) continue;
                if (IsOnBallPlate(b)) continue; // needs an explicit E press
                Grab(b);
                return;
            }
        }

        static bool IsOnBallPlate(Basketball b)
        {
            foreach (var plate in PressurePlate.All)
            {
                if (plate == null || plate.Kind != PlateKind.Ball) continue;
                var d = b.transform.position - plate.transform.position;
                if (new Vector2(d.x, d.z).magnitude < 0.75f && d.y < 0.8f && d.y > -0.1f) return true;
            }
            return false;
        }

        void FindPlateBall()
        {
            PlateBallNearby = null;
            foreach (var b in Basketball.All)
            {
                if (b == null || b.IsHeld || !b.PlayerCanGrab || !IsOnBallPlate(b)) continue;
                var d = b.transform.position - transform.position;
                if (new Vector2(d.x, d.z).magnitude < 1.8f)
                {
                    PlateBallNearby = b;
                    return;
                }
            }
        }

        public void GrabPlateBall()
        {
            if (Ball == null && PlateBallNearby != null) Grab(PlateBallNearby);
        }

        void Grab(Basketball b)
        {
            Ball = b;
            b.Hold(this);
            GameAudio.Play("pickup", 0.6f);
            _dribbleT = 0;
        }

        // ------------------------------------------------------------- aiming --
        void UpdateAim()
        {
            AimTarget = null;
            AimPoint = null;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position, camFwd = cam.transform.forward;
            Vector3 from = ReleasePoint;
            float best = 32f;

            foreach (var h in Hoop.All)
            {
                if (h == null || !h.AimTarget) continue;
                var target = h.RimCenter;
                if (Flat(target - transform.position) > 34f) continue;
                float ang = Vector3.Angle(camFwd, target - camPos);
                if (ang >= best || !Visible(from, target + Vector3.up * 0.55f, h.transform)) continue;
                best = ang;
                AimTarget = h;
                AimPoint = target;
            }
            foreach (var rb in ReboundBoard.AllBoards)
            {
                if (rb == null || !rb.Aimable) continue;
                var target = rb.FaceCenter;
                if (Flat(target - transform.position) > 30f) continue;
                float ang = Vector3.Angle(camFwd, target - camPos);
                if (ang >= best || !Visible(from, target, rb.transform)) continue;
                best = ang;
                AimTarget = rb;
                AimPoint = target;
            }
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static bool Visible(Vector3 from, Vector3 to, Transform owner)
        {
            if (Physics.Linecast(from, to, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(owner);
            return true;
        }

        // -------------------------------------------------------------- dunks --
        void UpdateDunk()
        {
            DunkHoop = null;
            if (_pc.Grounded) return;
            var p = transform.position;
            foreach (var h in Hoop.All)
            {
                if (h == null || h.Mode == HoopMode.Counter && !h.AcceptPlayerShots) continue;
                var rim = h.RimCenter;
                if (Flat(rim - p) > 1.7f) continue;
                if (p.y + 2.0f < rim.y - 0.35f) continue;   // can't reach yet
                if (p.y > rim.y + 0.3f) continue;           // flew way over it
                DunkHoop = h;
                return;
            }
        }

        System.Collections.IEnumerator DoDunk(Hoop hoop)
        {
            var ball = Ball;
            var rim = hoop.RimCenter;
            var toPlayer = transform.position - rim;
            toPlayer.y = 0;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = hoop.transform.forward;
            toPlayer = toPlayer.normalized;
            var hangAt = rim + toPlayer * 0.55f;
            hangAt.y = rim.y - 1.7f;
            _pc.Hang(hangAt, 0.55f);
            _pc.Anim?.Trigger("Dunk");
            Charging = false;
            yield return new WaitForSeconds(0.12f);
            if (ball == null || Ball != ball) yield break;
            Ball = null;
            ball.Release(rim + Vector3.up * 0.3f, Vector3.down * 5f, BallOwner.Player);
            ball.IsDunk = true;
            _pickupCooldown = Time.time + 0.8f;
            _pc.Cam?.Shake(0.35f);
            Hud.Toast("SLAM DUNK!", new Color(1f, 0.8f, 0.2f));
        }

        // ------------------------------------------------------------- shooting --
        void Shoot()
        {
            Charging = false;
            var ball = Ball;
            if (ball == null) return;
            Ball = null;
            _pickupCooldown = Time.time + 0.6f;
            var from = ReleasePoint;
            float power = Power;

            ShotQuality q;
            if (SuperShots || (power >= SweetMin && power <= SweetMax)) q = ShotQuality.Perfect;
            else if (power >= OkMin && power <= OkMax) q = ShotQuality.Close;
            else q = power < OkMin ? ShotQuality.Short : ShotQuality.Long;

            Vector3 v;
            if (AimTarget is Hoop hoop)
            {
                var target = hoop.RimCenter;
                var flatDir = target - from;
                flatDir.y = 0;
                flatDir.Normalize();
                var side = Vector3.Cross(Vector3.up, flatDir);
                switch (q)
                {
                    case ShotQuality.Close:
                        target += (Random.value < 0.5f ? side : flatDir) * (Random.value < 0.5f ? -1 : 1) * Random.Range(0.2f, 0.3f);
                        break;
                    case ShotQuality.Short:
                        target -= flatDir * Mathf.Lerp(1.6f, 0.7f, power / OkMin);
                        break;
                    case ShotQuality.Long:
                        target += flatDir * 0.9f + Vector3.up * 0.3f;
                        break;
                }
                v = Ballistics.SolveLeading(from, target, hoop.Velocity, Ballistics.ShotArc(from, target), out _);
            }
            else if (AimTarget is ReboundBoard board)
            {
                var target = board.FaceCenter + board.Facing * 0.1f;
                if (q == ShotQuality.Short) target += Vector3.down * 1.4f;
                if (q == ShotQuality.Long) target += Vector3.up * 1.3f;
                v = Ballistics.Solve(from, target, Mathf.Clamp(Flat(target - from) * 0.12f, 0.6f, 2.5f), out _);
            }
            else
            {
                q = ShotQuality.Free;
                var cam = _pc.Cam != null ? _pc.Cam.transform : transform;
                var fwd = cam.forward;
                fwd.y = 0;
                fwd.Normalize();
                float pitch = Mathf.Clamp((_pc.Cam != null ? _pc.Cam.Pitch : 15f) + 30f, 25f, 70f) * Mathf.Deg2Rad;
                v = (fwd * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * Mathf.Lerp(6f, 14f, power);
            }

            ball.Release(from, v, BallOwner.Player);
            _pc.Anim?.Trigger("Shoot");
            GameAudio.Play("throw", 0.8f);
            switch (q)
            {
                case ShotQuality.Perfect: Hud.Toast("PERFECT!", new Color(0.4f, 1f, 0.5f), 0.9f); break;
                case ShotQuality.Close: Hud.Toast("Close one!", new Color(1f, 0.9f, 0.4f), 0.9f); break;
                case ShotQuality.Short: Hud.Toast("Too soft!", new Color(1f, 0.6f, 0.4f), 0.9f); break;
                case ShotQuality.Long: Hud.Toast("Too hard!", new Color(1f, 0.6f, 0.4f), 0.9f); break;
            }
        }

        // ------------------------------------------------------- drop / place --
        void DropOrPlace()
        {
            var ball = Ball;
            PressurePlate bestPlate = null;
            float best = 2.6f;
            foreach (var plate in PressurePlate.All)
            {
                if (plate == null || plate.Kind != PlateKind.Ball) continue;
                float d = Flat(plate.transform.position - transform.position);
                if (d < best && Mathf.Abs(plate.transform.position.y - transform.position.y) < 1.5f)
                {
                    best = d;
                    bestPlate = plate;
                }
            }
            Ball = null;
            _pickupCooldown = Time.time + 1.0f;
            if (bestPlate != null)
            {
                ball.Release(bestPlate.BallRestPoint, Vector3.zero, BallOwner.Player);
                GameAudio.Play("bounce", 0.7f);
                Hud.Toast("Ball placed!", PressurePlate.KindColor(PlateKind.Ball), 0.8f);
            }
            else
            {
                var fwd = _pc.Forward;
                ball.Release(transform.position + Vector3.up * 0.8f + fwd * 0.55f, fwd * 1.5f + Vector3.up * 1.5f,
                    BallOwner.Player);
            }
        }

        /// <summary>Lose the ball (knocked down).</summary>
        public void DropBall(bool scatter)
        {
            if (Ball == null) return;
            var ball = Ball;
            Ball = null;
            Charging = false;
            _pickupCooldown = Time.time + 1.2f;
            var v = scatter ? new Vector3(Random.Range(-3f, 3f), 4f, Random.Range(-3f, 3f)) : Vector3.zero;
            ball.Release(transform.position + Vector3.up * 1.0f, v, BallOwner.None);
        }

        public void ForgetBall()
        {
            Ball = null;
            Charging = false;
        }
    }
}
