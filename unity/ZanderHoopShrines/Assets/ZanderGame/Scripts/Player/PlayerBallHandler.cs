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

            if (Dunking) return;
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

            if (Dunking) return;
            UpdateAim();
            UpdateDunk();


            if (Controls.DunkDown && !_pc.IsStunned && !Charging)
            {
                if (DunkHoop != null)
                {
                    StartCoroutine(DunkRoutine(DunkHoop));
                    return;
                }
                TooFarHint();
            }

            if (Controls.ShootDown && !_pc.IsStunned)
            {
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
                pos = p - right * 0.26f + fwd * 0.34f;   // Zander dribbles with his LEFT hand
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

        // ------------------------------------------------------------ stealing --
        float _stealCooldown;

        /// <summary>Someone (King Swat or a sparring bot) is dribbling right next to Zander, who has no ball.</summary>
        public BallCarrier StealTarget
        {
            get
            {
                if (Ball != null || Dunking || Time.time <= _stealCooldown) return null;
                BallCarrier best = null;
                float bestD = 2.6f;
                foreach (var c in BallCarrier.All)
                {
                    if (c == null || c.HeldBall == null) continue;
                    float d = Flat(c.transform.position - transform.position);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = c;
                    }
                }
                return best;
            }
        }

        public bool CanStealFromBoss => StealTarget != null;

        public bool TrySteal()
        {
            var target = StealTarget;
            if (target == null) return false;
            _stealCooldown = Time.time + 1.2f;
            _pc.Anim?.Trigger("Steal");
            _pc.Lunge(target.transform.position - transform.position, 5f, 0.18f);
            GameAudio.Play("throw", 0.7f, 1.3f);
            float chance = Mathf.Min(1f, target.StealChance + (SuperShots ? 0.2f : 0f));
            if (UnityEngine.Random.value < chance)
            {
                var b = target.LoseBall();
                if (b != null)
                {
                    Grab(b);
                    GameAudio.Play("chime", 0.7f, 1.3f);
                    Rumble.Pulse(0.3f, 0.6f, 0.2f);
                    Hud.Toast("STOLEN! Go, go, go!", new Color(0.5f, 1f, 0.6f), 1.2f);
                }
            }
            else
            {
                GameAudio.Play("buzz", 0.4f, 1.4f);
                Hud.Toast("Missed the steal - try again!", Color.white, 1.0f);
            }
            return true;
        }

        /// <summary>Hand Zander a ball (boss-contest inbound).</summary>
        public void GiveBall(Basketball b)
        {
            if (b == null) return;
            if (Ball != null && Ball != b) DropBall(false);
            if (Ball == b) return;
            Grab(b);
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
                // a board whose laser is locked onto a bank-shot hoop can also be picked by
                // aiming at THAT hoop: the ball goes off the board and in
                if (rb.FindTarget(out var via) is Hoop bankHoop && bankHoop.RequiresRebound)
                    ang = Mathf.Min(ang, Vector3.Angle(camFwd, via - camPos));
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
        public const float DunkReach = 5.0f;     // how far (flat) from the rim Zander can leap
        public const float DunkMaxRise = 3.6f;   // how high above his feet a rim can be
        public const float DunkMissChance = 0.10f;
        public const float HangHandHeight = 1.15f; // hands above feet in the RimHang pose

        public bool Dunking { get; private set; }
        public bool RimHanging { get; private set; }
        float _dunkHintAt;

        void UpdateDunk()
        {
            DunkHoop = null;
            if (Dunking) return;
            var p = transform.position;
            float reach = _pc.Grounded ? DunkReach : DunkReach + 1f;
            float best = float.MaxValue;
            foreach (var h in Hoop.All)
            {
                if (h == null || !h.isActiveAndEnabled || h.RequiresRebound || !h.AcceptPlayerShots) continue;
                var rim = h.RimCenter;
                float flat = Flat(rim - p);
                float rise = rim.y - p.y;
                if (flat > reach || rise > DunkMaxRise || rise < -1.0f) continue;
                if (flat < best)
                {
                    best = flat;
                    DunkHoop = h;
                }
            }
        }

        void TooFarHint()
        {
            if (Time.time < _dunkHintAt) return;
            _dunkHintAt = Time.time + 1.5f;
            var p = transform.position;
            foreach (var h in Hoop.All)
            {
                if (h == null || h.RequiresRebound || !h.AcceptPlayerShots) continue;
                if (Flat(h.RimCenter - p) < DunkReach + 1f && h.RimCenter.y - p.y > DunkMaxRise)
                {
                    Hud.Toast("Too high! Get up higher - jump onto a platform or use a BOUNCE pad - then press {DUNK}!",
                        new Color(0.7f, 0.5f, 1f), 1.8f);
                    return;
                }
            }
            Hud.Toast("Get closer to a hoop to dunk!", Color.white, 1.2f);
        }

        /// <summary>Where Zander hangs: in front of the rim, hands on it.</summary>
        static Vector3 HangPoint(Hoop hoop, out Vector3 front)
        {
            front = hoop.transform.forward;
            front.y = 0f;
            front = front.sqrMagnitude > 0.001f ? front.normalized : Vector3.forward;
            return hoop.RimCenter + front * 0.42f + Vector3.down * HangHandHeight;
        }

        static float Smooth(float k) => k * k * (3f - 2f * k);

        System.Collections.IEnumerator DunkRoutine(Hoop hoop)
        {
            var ball = Ball;
            Dunking = true;
            Charging = false;
            _pc.BeginScripted();
            _pc.Anim?.SetDunkPhase(1);
            GameAudio.Play("throw", 0.9f, 0.8f);

            // 1) leap to the rim (follows the rim if it moves)
            Vector3 start = transform.position;
            HangPoint(hoop, out var front0);
            float dur = Mathf.Clamp(0.32f + Flat(hoop.RimCenter - start) * 0.07f, 0.35f, 0.75f);
            float t = 0f;
            while (t < dur)
            {
                if (!_pc.Scripted) { EndDunk(); yield break; }
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                var hang = HangPoint(hoop, out var front);
                var pos = Vector3.Lerp(start, hang, Smooth(k));
                pos.y += Mathf.Sin(k * Mathf.PI) * 0.9f;      // a little hop over the top
                _pc.ScriptedPose(pos, -front);
                yield return null;
            }

            // 2) the slam: 90% it goes down, 10% it rims out (decided only now, truly at random)
            bool makesIt = UnityEngine.Random.value >= DunkMissChance;
            _pc.Anim?.SetDunkPhase(2);
            var rim = hoop.RimCenter;
            HangPoint(hoop, out var f);
            if (ball != null && Ball == ball)
            {
                Ball = null;
                if (makesIt)
                {
                    ball.Release(rim + Vector3.up * 0.32f, Vector3.down * 5f, BallOwner.Player);
                    ball.IsDunk = true;
                    Hud.Toast("SLAM DUNK!", new Color(1f, 0.8f, 0.2f));
                }
                else
                {
                    // jammed onto the front of the rim: it bounces back out
                    var side = Vector3.Cross(Vector3.up, f) * UnityEngine.Random.Range(-1f, 1f);
                    ball.Release(rim + f * (hoop.RimRadius + 0.05f) + Vector3.up * 0.2f,
                        f * 2.6f + Vector3.up * 3.2f + side * 1.2f, BallOwner.Player);
                    ball.IsDunk = true;
                    GameAudio.PlayAt("rim", rim, 1f, 0.85f);
                    Rumble.Pulse(0.5f, 0.2f, 0.2f);
                    Hud.Toast("CLANK! Off the rim!", new Color(1f, 0.6f, 0.4f), 1.2f);
                }
                _pickupCooldown = Time.time + 1.0f;
            }
            _pc.Cam?.Shake(0.35f);

            // 3) hang on the rim for as long as Dunk is held
            RimHanging = true;
            float hangT = 0f;
            while (_pc.Scripted && hangT < 12f && (hangT < 0.35f || Controls.DunkHeld))
            {
                hangT += Time.deltaTime;
                var hang = HangPoint(hoop, out var front);
                hang += front * Mathf.Sin(hangT * 3f) * 0.03f;   // gentle swing
                _pc.ScriptedPose(hang, -front);
                hoop.AddHangTime(Time.deltaTime);
                if (hangT > 0.4f) _pc.Anim?.SetDunkPhase(3);
                yield return null;
            }
            hoop.EndHang();
            HangPoint(hoop, out var fEnd);
            EndDunk();
            _pc.EndScripted(fEnd * 2.5f);
        }

        void EndDunk()
        {
            Dunking = false;
            RimHanging = false;
            _pc.Anim?.SetDunkPhase(0);
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
                // bank shots are about solving the puzzle, not timing: always on target
                var target = board.FaceCenter + board.Facing * 0.1f;
                q = ShotQuality.Perfect;
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
            Rumble.Pulse(0.1f, q == ShotQuality.Perfect ? 0.5f : 0.2f, 0.1f);
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
