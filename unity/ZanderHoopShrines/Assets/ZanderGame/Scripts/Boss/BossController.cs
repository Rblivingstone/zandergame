using System.Collections;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// KING SWAT - the boss. During the shooting contest he:
    ///   phase 1: wanders and shoots at his own hoop,
    ///   phase 2: also STOMPS, sending out shockwave rings Zander must jump over,
    ///   phase 3: also guards Zander's hoop and SWATS shots away (arms up = wait!).
    /// </summary>
    public class BossController : BallCarrier
    {
        public const float Height = 3.4f;
        public BossArena Arena;
        public ShootingContest Contest;
        public Transform Visual;
        public bool Blocking { get; private set; }
        public string State { get; private set; } = "idle";

        ClipAnimator _clips;
        CharacterRig _rig;
        Transform _hand;
        Vector3 _moveTarget;
        bool _moving;
        Coroutine _brain;
        float _lastShot, _lastStomp, _lastBlock;
        float _t;
        Quaternion _face;
        string _oneShot;
        float _oneShotUntil;

        public static BossController Spawn(BossArena arena, ShootingContest contest)
        {
            var go = new GameObject("KingSwat");
            go.transform.position = arena.BossSpawn;
            var boss = go.AddComponent<BossController>();
            boss.Arena = arena;
            boss.Contest = contest;
            boss.Visual = CharacterFactory.CreateBoss(go.transform, Height);
            boss.Visual.localRotation = Quaternion.Euler(0, 180, 0);
            boss._face = boss.Visual.rotation;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.95f;
            col.height = Height;
            col.center = new Vector3(0, Height / 2, 0);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            boss._clips = ClipAnimator.TryCreate(boss.Visual.gameObject, "Models/KingSwat");
            boss._rig = boss.Visual.GetComponent<CharacterRig>();
            boss._hand = ModelLibrary.FindDeep(boss.Visual, "hand.R");
            if (boss._hand == null && boss._rig != null) boss._hand = boss._rig.HandR;
            Build.Label("KING SWAT", go.transform, new Vector3(0, Height + 0.9f, 0), 0.5f, new Color(1f, 0.8f, 0.2f), 0, true);
            return boss;
        }

        Vector3 HandPos => _hand != null ? _hand.position : transform.position + Vector3.up * 3.6f + Visual.forward * 1.2f;

        // ------------------------------------------------------------ control --
        void Awake() => Instance = this;
        public override float StealChance => 0.55f;

        public void ResetToSpawn()
        {
            StopBrain();
            if (HeldBall != null)
            {
                Destroy(HeldBall.gameObject);
                _held = null;
            }
            transform.position = Arena.BossSpawn;
            Blocking = false;
            _moving = false;
            FaceDir(Vector3.back);
            Base("Idle");
        }

        public void StartBrain()
        {
            StopBrain();
            _lastShot = _lastStomp = _lastBlock = Time.time;
            _brain = StartCoroutine(Brain());
        }

        public void StopBrain()
        {
            if (_brain != null) StopCoroutine(_brain);
            _brain = null;
            Blocking = false;
            _moving = false;
        }

        public void PlayTaunt()
        {
            PlayOnce("Taunt", 1.4f);
            GameAudio.Play("roar");
        }

        public void PlayHit()
        {
            if (Blocking) return;
            PlayOnce("Hit", 0.8f);
        }

        public void PlayDefeat()
        {
            StopBrain();
            if (HeldBall != null)
            {
                HeldBall.Release(HeldBall.transform.position, Vector3.up * 2f, BallOwner.None);
                _held = null;
            }
            _clips?.PlayAndHold("Defeat");
            State = "defeated";
            _oneShot = "Defeat";
            _oneShotUntil = float.MaxValue;
            GameAudio.Play("roar", 0.8f, 0.7f);
        }

        // -------------------------------------------------------------- brain --
        // King Swat plays for real: he fetches balls from the racks (and grabs loose
        // rebounds), dribbles to his hoop and shoots or dunks, chases Zander to STEAL
        // the ball, and in later rounds stomps shockwaves and guards Zander's hoop.

        public static BossController Instance { get; private set; }
        public override Basketball HeldBall => _held;
        Basketball _held;
        float _moveSpeed = 3.2f;
        float _nextSteal;
        float _dribbleT;
        float _lastBounce;

        bool PlayerHasBall => PlayerController.Instance != null && PlayerController.Instance.Balls != null &&
                              PlayerController.Instance.Balls.HasBall;

        float FlatDist(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0;
            return d.magnitude;
        }

        float _backOffUntil;

        /// <summary>Teleport next to a hoop holding the ball (inbound after Zander scores).</summary>
        public void InboundAt(Vector3 spot, Vector3 facing, Basketball ball)
        {
            StopBrain();
            transform.position = new Vector3(spot.x, 0f, spot.z);
            FaceDir(facing);
            Visual.rotation = _face;
            if (ball != null)
            {
                var player = PlayerController.Instance;
                if (player != null && player.Balls != null && player.Balls.Ball == ball) player.Balls.ForgetBall();
                TakeBall(ball);
            }
            _brain = StartCoroutine(Brain());
        }

        /// <summary>Give Zander a moment after his inbound before pressing.</summary>
        public void BackOff(float seconds)
        {
            _backOffUntil = Time.time + seconds;
            StopBrain();
            _brain = StartCoroutine(Brain());
        }

        IEnumerator Brain()
        {
            _nextSteal = Time.time + 2.5f;
            yield return new WaitForSeconds(0.4f);
            while (true)
            {
                int phase = Contest.Phase;
                if (Contest.Inbounding)
                {
                    _moving = false;
                    State = "idle";
                }
                else if (HeldBall != null)
                    yield return Attack(phase);                 // his ball: go score
                else if (PlayerHasBall)
                {
                    if (phase >= 2 && Time.time - _lastStomp > 11f && FlatDist(PlayerPos) > 4f && FlatDist(PlayerPos) < 10f)
                    {
                        yield return Stomp();
                        _lastStomp = Time.time;
                    }
                    else yield return Defend(phase);            // Zander's ball: lock him down
                }
                else
                {
                    var ball = FindBall();
                    if (ball != null) yield return Fetch(ball);  // loose ball: chase it
                    else _moving = false;
                }
                yield return null;
            }
        }

        /// <summary>
        /// Tight man-to-man defence: stay between Zander and his hoop, hands up when he
        /// shoots (and swat it if he's close), and lunge for steals every few seconds.
        /// </summary>
        IEnumerator Defend(int phase)
        {
            State = "defend";
            float speed = phase >= 3 ? 6.0f : phase == 2 ? 5.5f : 5.0f;   // Zander walks 5.2, sprints 8.2
            float stealEvery = phase >= 3 ? 2.2f : phase == 2 ? 2.8f : 3.4f;
            float handsUpUntil = 0f;
            var player = PlayerController.Instance;
            while (PlayerHasBall && HeldBall == null && !Contest.Inbounding)
            {
                var pp = PlayerPos;
                var rim = Arena.PlayerHoop.RimCenter;
                var toHoop = rim - pp;
                toHoop.y = 0;
                float hoopDist = toHoop.magnitude;
                toHoop = hoopDist > 0.01f ? toHoop / hoopDist : Vector3.forward;
                bool backingOff = Time.time < _backOffUntil;
                float gap = backingOff ? 4.5f : Mathf.Min(1.9f, Mathf.Max(1.2f, hoopDist - 0.5f));
                var guard = pp + toHoop * gap;
                _moveTarget = new Vector3(guard.x, 0, guard.z);
                _moveSpeed = speed;
                _moving = FlatDist(_moveTarget) > 0.15f;
                FaceDir(pp - transform.position);

                // hands up when Zander gets a shot off near him
                bool charging = player != null && player.Balls != null && player.Balls.Charging;
                if (charging && FlatDist(pp) < 4.5f) handsUpUntil = Time.time + 0.9f;
                bool handsUp = Time.time < handsUpUntil;
                if (handsUp != Blocking)
                {
                    Blocking = handsUp;
                    if (handsUp) Base("Block");
                }

                // steal attempts (never while he is mid-shot, that's what the hands are for)
                if (!backingOff && !charging && Time.time > _nextSteal && FlatDist(pp) < 2.4f)
                {
                    _moving = false;
                    Blocking = false;
                    PlayOnce("Steal", 0.9f);
                    Hud.Toast("King Swat goes for the STEAL!", new Color(0.8f, 0.6f, 1f), 0.9f);
                    yield return new WaitForSeconds(0.35f);
                    float chance = phase >= 3 ? 0.5f : phase == 2 ? 0.42f : 0.35f;
                    if (PlayerBallHandler.SuperShots) chance -= 0.2f;
                    if (PlayerHasBall && FlatDist(PlayerPos) < 2.7f && Random.value < chance)
                    {
                        var pb = player.Balls;
                        var b = pb.Ball;
                        pb.ForgetBall();
                        TakeBall(b);
                        GameAudio.Play("dunk", 0.8f, 1.4f);
                        Rumble.Pulse(0.6f, 0.4f, 0.25f);
                        Hud.Toast("STOLEN by King Swat!", new Color(1f, 0.5f, 0.5f), 1.2f);
                    }
                    else if (PlayerHasBall) Hud.Toast("Nice dodge!", new Color(0.5f, 1f, 0.6f), 0.9f);
                    _nextSteal = Time.time + stealEvery;
                    yield return new WaitForSeconds(0.3f);
                    continue;
                }
                yield return null;
            }
            // the shot is in the air: keep the hands up a moment to try the swat
            while (Time.time < handsUpUntil && HeldBall == null)
            {
                _moving = false;
                SwatNearbyBalls();
                yield return null;
            }
            Blocking = false;
            _moving = false;
        }

        IEnumerator WalkTo(Vector3 target, float maxSeconds, float speed = 3.2f)
        {
            if (State != "chase") State = "walk";
            _moveTarget = target;
            _moveSpeed = speed;
            _moving = true;
            float until = Time.time + maxSeconds;
            while (_moving && Time.time < until) yield return null;
            _moving = false;
        }

        /// <summary>Nearest ball he can go for: on a rack or lying loose on the court.</summary>
        Basketball FindBall()
        {
            Basketball best = null;
            float bestD = float.MaxValue;
            foreach (var b in Basketball.All)
            {
                if (b == null || b.IsHeld || !b.PlayerCanGrab || !Arena.Room.Contains(b.transform.position)) continue;
                var p = b.transform.position;
                if (Mathf.Abs(p.x) > 14.5f || p.y > 2.5f) continue;
                // let Zander's shots fly; only chase them once they've come down
                if (!b.IsDocked && b.LastThrower == BallOwner.Player && Time.time - b.ReleaseTime < 1.0f) continue;
                float d = FlatDist(p) * (b.IsDocked ? 1.25f : 1f);   // slight preference for loose balls
                if (d < bestD)
                {
                    bestD = d;
                    best = b;
                }
            }
            return best;
        }

        IEnumerator Fetch(Basketball ball)
        {
            State = "fetch";
            float until = Time.time + 5f;
            _moveSpeed = 6.0f;
            while (Time.time < until && ball != null && !ball.IsHeld && ball.PlayerCanGrab && !Contest.Inbounding)
            {
                var p = ball.transform.position;
                if (FlatDist(p) < 1.4f && p.y < 2.6f)
                {
                    TakeBall(ball);
                    break;
                }
                _moveTarget = new Vector3(p.x, 0, p.z);
                _moving = true;
                yield return null;
            }
            _moving = false;
        }

        void TakeBall(Basketball ball)
        {
            ball.Hold(this);
            _held = ball;
            _dribbleT = 0f;
            GameAudio.PlayAt("pickup", transform.position, 0.8f, 0.7f);
        }

        /// <summary>Zander stole it! Hands the ball over.</summary>
        public override Basketball LoseBall()
        {
            var b = HeldBall;
            _held = null;
            if (State == "shoot") State = "idle";
            PlayOnce("Hit", 0.8f);
            _nextSteal = Time.time + 2.5f;
            return b;
        }

        IEnumerator Attack(int phase)
        {
            State = "attack";
            var hoop = Arena.BossHoop;
            var rim = hoop.RimCenter;
            var front = hoop.transform.forward;
            front.y = 0;
            front.Normalize();
            var side = Vector3.Cross(Vector3.up, front);
            bool dunk = phase >= 3 ? Random.value < 0.45f : phase == 2 && Random.value < 0.2f;
            var spot = dunk ? rim + front * 1.3f : rim + front * Random.Range(3.5f, 6.5f) + side * Random.Range(-4f, 4f);
            spot.y = 0;
            float until = Time.time + 12f;     // long enough to dribble the full court after an inbound
            _moveTarget = spot;
            _moveSpeed = 4.2f;
            _moving = true;
            while (_moving && Time.time < until && HeldBall != null) yield return null;
            _moving = false;
            if (HeldBall == null) yield break;   // stolen on the way!
            if (dunk) yield return BossDunk(hoop);
            else yield return Shoot(phase);
        }

        IEnumerator Shoot(int phase)
        {
            State = "shoot";
            var hoop = Arena.BossHoop;
            FaceDir(hoop.RimCenter - transform.position);
            PlayOnce("Shoot", 1.2f);
            float t = 0;
            while (t < 0.42f)
            {
                t += Time.deltaTime;
                if (HeldBall == null) yield break;
                HeldBall.transform.position = HandPos + Vector3.up * 0.35f;
                yield return null;
            }
            var ball = HeldBall;
            _held = null;
            if (ball != null)
            {
                float makeChance = phase == 1 ? 0.5f : phase == 2 ? 0.55f : 0.6f;
                if (PlayerBallHandler.SuperShots) makeChance -= 0.15f;
                var target = hoop.RimCenter;
                if (Random.value > makeChance)
                {
                    var off = Random.insideUnitCircle.normalized * Random.Range(0.45f, 0.7f);
                    target += new Vector3(off.x, 0.05f, off.y);
                }
                var from = HandPos + Vector3.up * 0.5f;
                var v = Ballistics.Solve(from, target, Ballistics.ShotArc(from, target), out _);
                ball.Release(from, v, BallOwner.Boss);
                GameAudio.PlayAt("throw", from, 1f, 0.7f);
            }
            yield return new WaitForSeconds(0.7f);
            State = "idle";
        }

        IEnumerator BossDunk(Hoop hoop)
        {
            State = "shoot";
            FaceDir(hoop.RimCenter - transform.position);
            PlayOnce("Shoot", 1.0f);
            Hud.Toast("King Swat goes for the DUNK!", new Color(0.8f, 0.6f, 1f), 1.0f);
            float t = 0;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                if (HeldBall == null) yield break;
                HeldBall.transform.position = Vector3.Lerp(HandPos, hoop.RimCenter + Vector3.up * 0.6f, t / 0.35f);
                yield return null;
            }
            var ball = HeldBall;
            _held = null;
            var rim = hoop.RimCenter;
            if (Random.value < 0.85f)
                ball.Release(rim + Vector3.up * 0.32f, Vector3.down * 5f, BallOwner.Boss);
            else
                ball.Release(rim + hoop.transform.forward * 0.4f + Vector3.up * 0.2f,
                    hoop.transform.forward * 3f + Vector3.up * 3f, BallOwner.Boss);
            ball.IsDunk = true;
            GameAudio.Play("dunk", 1f, 0.8f);
            GameManager.Instance?.Cam?.Shake(0.3f);
            yield return new WaitForSeconds(0.8f);
            State = "idle";
        }

        IEnumerator Stomp()
        {
            State = "stomp";
            FaceDir(PlayerPos - transform.position);
            Hud.Toast("KING SWAT is going to STOMP! Get ready to JUMP!", new Color(1f, 0.5f, 0.3f), 1.4f);
            PlayOnce("Stomp", 1.3f);
            yield return new WaitForSeconds(0.6f);
            GameAudio.Play("stomp");
            Rumble.Pulse(0.6f, 0.15f, 0.35f);
            GameManager.Instance?.Cam?.Shake(0.5f);
            Shockwave.Spawn(transform.position, Contest.Phase >= 3 ? 8f : 6.5f);
            Effects.Burst(transform.position + Vector3.up * 0.2f, new Color(0.8f, 0.6f, 1f), 30, 6f, 0.2f, 0.7f);
            yield return new WaitForSeconds(0.7f);
        }

        readonly System.Collections.Generic.HashSet<Basketball> _swatRolled = new System.Collections.Generic.HashSet<Basketball>();
        float _swatRollReset;

        void SwatNearbyBalls()
        {
            if (Time.time > _swatRollReset) _swatRolled.Clear();
            var reach = transform.position + Vector3.up * (Height + 0.4f);
            int phase = Contest != null ? Contest.Phase : 1;
            float chance = phase >= 3 ? 0.5f : phase == 2 ? 0.4f : 0.3f;
            if (PlayerBallHandler.SuperShots) chance -= 0.2f;
            foreach (var b in Basketball.All.ToArray())
            {
                if (b == null || b.IsHeld || b.IsDunk || b.LastThrower != BallOwner.Player) continue;
                if ((b.transform.position - reach).magnitude > 3.0f) continue;
                if (_swatRolled.Contains(b)) continue;
                _swatRolled.Add(b);
                _swatRollReset = Time.time + 2f;
                if (Random.value > chance) continue;       // he got a fingertip on air - shot is still good
                var away = b.transform.position - transform.position;
                away.y = 0;
                b.LastThrower = BallOwner.None;
                b.Body.SetVelocity(away.normalized * 7f + Vector3.up * 3f);
                GameAudio.PlayAt("dunk", b.transform.position, 1f, 1.3f);
                Effects.Burst(b.transform.position, new Color(0.8f, 0.4f, 1f), 16, 4f);
                Hud.Toast("SWATTED by King Swat!", new Color(0.8f, 0.6f, 1f), 1.2f);
            }
        }

        Vector3 PlayerPos => PlayerController.Instance != null ? PlayerController.Instance.transform.position : transform.position;

        void FaceDir(Vector3 d)
        {
            d.y = 0;
            if (d.sqrMagnitude > 0.001f) _face = Quaternion.LookRotation(d);
        }

        int _facingChecks;

        void LateUpdate()
        {
            // dribble the ball while carrying it
            if (HeldBall != null && State != "shoot")
            {
                _dribbleT += Time.deltaTime;
                var fwd = Visual.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                var hand = HandPos;
                var pos = transform.position + fwd * 0.9f + right * 0.85f;
                float top = Mathf.Max(hand.y, transform.position.y + 1.1f);
                float k = Mathf.Abs(Mathf.Cos(Mathf.PI * _dribbleT / 0.5f));
                pos.y = transform.position.y + Basketball.Radius + (top - transform.position.y - Basketball.Radius) * k;
                if (k < 0.12f && _dribbleT - _lastBounce > 0.3f)
                {
                    _lastBounce = _dribbleT;
                    GameAudio.PlayAt("bounce", pos, 0.5f, 0.8f);
                }
                HeldBall.transform.position = pos;
            }
            if (_facingChecks >= 3 || Time.frameCount % 5 != 0) return;
            _facingChecks++;
            if (ModelLibrary.FaceByFeet(Visual.gameObject, "BossBoot")) _facingChecks = 3;
        }

        // ------------------------------------------------------- per frame --
        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_moving)
            {
                var to = _moveTarget - transform.position;
                to.y = 0;
                if (to.magnitude < 0.2f) _moving = false;
                else
                {
                    transform.position += to.normalized * Mathf.Min(to.magnitude, _moveSpeed * dt);
                    if (State != "defend") FaceDir(to);
                }
            }
            else if (State == "idle" || State == "walk")
                FaceDir(PlayerPos - transform.position);

            Visual.rotation = Quaternion.RotateTowards(Visual.rotation, _face, 300f * dt);

            if (State != "defeated")
            {
                if (!Blocking) Base(_moving ? "Walk" : "Idle");
                else SwatNearbyBalls();
            }

            if (_clips != null) _clips.Tick(dt);
            else if (_rig != null) Procedural();
        }

        void Base(string n) => _clips?.SetBase(n);

        void PlayOnce(string n, float seconds)
        {
            _clips?.PlayOnce(n);
            _oneShot = n;
            _oneShotUntil = Time.time + seconds;
        }

        void Procedural()
        {
            float s = Mathf.Sin(_t * 5f);
            bool one = Time.time < _oneShotUntil;
            float walk = _moving ? 1f : 0f;
            _rig.LegL.localRotation = Quaternion.Euler(30 * s * walk, 0, 0);
            _rig.LegR.localRotation = Quaternion.Euler(-30 * s * walk, 0, 0);
            Quaternion armL = Quaternion.Euler(-25 * s * walk, 0, -10), armR = Quaternion.Euler(25 * s * walk, 0, 10);
            if (Blocking || (one && (_oneShot == "Taunt")))
            {
                armL = Quaternion.Euler(-170, 0, -15 + 10 * s);
                armR = Quaternion.Euler(-170, 0, 15 - 10 * s);
            }
            else if (one && _oneShot == "Shoot") armR = Quaternion.Euler(-165, 0, 10);
            else if (one && _oneShot == "Stomp")
                _rig.LegR.localRotation = Quaternion.Euler(-60 * Mathf.Clamp01((_oneShotUntil - Time.time) - 0.6f), 0, 0);
            _rig.ArmL.localRotation = armL;
            _rig.ArmR.localRotation = armR;
            if (State == "defeated")
            {
                _rig.Body.localRotation = Quaternion.Slerp(_rig.Body.localRotation, Quaternion.Euler(-70, 0, 0), Time.deltaTime * 2);
                _rig.Body.localPosition = Vector3.Lerp(_rig.Body.localPosition, new Vector3(0, -0.6f, 0), Time.deltaTime * 2);
            }
            else if (one && _oneShot == "Hit") _rig.Body.localRotation = Quaternion.Euler(-15, 0, 0);
            else _rig.Body.localRotation = Quaternion.identity;
        }

        void OnDestroy() => _clips?.Destroy();
    }

    /// <summary>An expanding shockwave ring. Jump over it or get knocked back!</summary>
    public class Shockwave : MonoBehaviour
    {
        float _radius = 0.5f;
        float _speed;
        float _max = 24f;
        bool _hit;
        LineRenderer _lr;
        Material _mat;
        const int Segments = 72;

        public static Shockwave Spawn(Vector3 center, float speed)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = new Vector3(center.x, 0.15f, center.z);
            var sw = go.AddComponent<Shockwave>();
            sw._speed = speed;
            sw._lr = go.AddComponent<LineRenderer>();
            sw._lr.positionCount = Segments + 1;
            sw._lr.useWorldSpace = true;
            sw._lr.widthMultiplier = 0.45f;
            sw._mat = Mat.Line(new Color(1f, 0.5f, 0.9f, 0.9f));
            sw._lr.sharedMaterial = sw._mat;
            return sw;
        }

        void Update()
        {
            _radius += _speed * Time.deltaTime;
            var c = transform.position;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2;
                _lr.SetPosition(i, c + new Vector3(Mathf.Cos(a) * _radius, 0.05f * Mathf.Sin(a * 6 + Time.time * 20), Mathf.Sin(a) * _radius));
            }
            float fade = 1f - _radius / _max;
            _mat.color = new Color(1f, 0.5f, 0.9f, Mathf.Clamp01(fade + 0.2f));

            var p = PlayerController.Instance;
            if (!_hit && p != null)
            {
                var d = p.transform.position - c;
                float feet = p.transform.position.y;
                d.y = 0;
                if (Mathf.Abs(d.magnitude - _radius) < 0.5f && feet < 0.45f)
                {
                    _hit = true;
                    p.Knockback(d, 9f);
                    Hud.Toast("Ouch! JUMP over the shockwave next time!", new Color(1f, 0.6f, 0.6f), 1.2f);
                }
            }
            if (_radius > _max) Destroy(gameObject);
        }
    }
}
