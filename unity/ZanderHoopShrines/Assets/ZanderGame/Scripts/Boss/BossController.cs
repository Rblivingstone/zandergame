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
    public class BossController : MonoBehaviour
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
        public void ResetToSpawn()
        {
            StopBrain();
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
            _clips?.PlayAndHold("Defeat");
            State = "defeated";
            _oneShot = "Defeat";
            _oneShotUntil = float.MaxValue;
            GameAudio.Play("roar", 0.8f, 0.7f);
        }

        // -------------------------------------------------------------- brain --
        IEnumerator Brain()
        {
            yield return new WaitForSeconds(1.0f);
            while (true)
            {
                int phase = Contest.Phase;
                float shotInterval = phase == 1 ? 6.0f : phase == 2 ? 5.5f : 5.0f;
                if (PlayerBallHandler.SuperShots) shotInterval += 1.5f;

                if (phase >= 2 && Time.time - _lastStomp > 8f)
                {
                    yield return Stomp();
                    _lastStomp = Time.time;
                }
                else if (phase >= 3 && Time.time - _lastBlock > 10f)
                {
                    yield return Block();
                    _lastBlock = Time.time;
                }
                else if (Time.time - _lastShot > shotInterval)
                {
                    yield return Shoot(phase);
                    _lastShot = Time.time;
                }
                else
                {
                    var c = Arena.CourtCenter;
                    yield return WalkTo(new Vector3(c.x + Random.Range(-7f, 7f), 0, c.z + Random.Range(-3f, 7f)), 2.5f);
                    State = "idle";
                    Base("Idle");
                    yield return new WaitForSeconds(Random.Range(0.4f, 1.2f));
                }
            }
        }

        IEnumerator WalkTo(Vector3 target, float maxSeconds)
        {
            State = "walk";
            _moveTarget = target;
            _moving = true;
            float until = Time.time + maxSeconds;
            while (_moving && Time.time < until) yield return null;
            _moving = false;
        }

        IEnumerator Shoot(int phase)
        {
            State = "shoot";
            var hoop = Arena.BossHoop;
            FaceDir(hoop.RimCenter - transform.position);
            var ball = Basketball.Create(HandPos);
            ball.PlayerCanGrab = false;
            ball.LastThrower = BallOwner.Boss;
            ball.Hold(this);
            PlayOnce("Shoot", 1.2f);
            float t = 0;
            while (t < 0.42f)
            {
                t += Time.deltaTime;
                if (ball != null) ball.transform.position = HandPos + Vector3.up * 0.35f;
                yield return null;
            }
            if (ball != null)
            {
                float makeChance = phase == 1 ? 0.55f : phase == 2 ? 0.6f : 0.65f;
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
                ball.ScheduleDespawn(4f);
                GameAudio.PlayAt("throw", from, 1f, 0.7f);
            }
            yield return new WaitForSeconds(0.9f);
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

        IEnumerator Block()
        {
            State = "block";
            var rim = Arena.PlayerHoop.RimCenter;
            var guard = new Vector3(rim.x, 0, rim.z - 3.4f);
            yield return WalkTo(guard, 3f);
            FaceDir(Vector3.back);
            Blocking = true;
            Base("Block");
            Hud.Toast("KING SWAT is guarding! Wait for his arms to drop!", new Color(0.8f, 0.6f, 1f), 1.6f);
            float until = Time.time + 3.5f;
            while (Time.time < until)
            {
                var hoopRim = Arena.PlayerHoop.RimCenter;
                var g = new Vector3(hoopRim.x, 0, hoopRim.z - 3.4f);
                transform.position = Vector3.MoveTowards(transform.position, g, 3f * Time.deltaTime);
                SwatNearbyBalls();
                yield return null;
            }
            Blocking = false;
            Base("Idle");
            yield return WalkTo(Arena.CourtCenter + new Vector3(Random.Range(-5f, 5f), 0, 4f), 3f);
        }

        void SwatNearbyBalls()
        {
            var reach = transform.position + Vector3.up * (Height + 0.4f);
            foreach (var b in Basketball.All.ToArray())
            {
                if (b == null || b.IsHeld || b.IsDunk || b.LastThrower != BallOwner.Player) continue;
                if ((b.transform.position - reach).magnitude > 3.0f) continue;
                var away = b.transform.position - transform.position;
                away.y = 0;
                b.LastThrower = BallOwner.None;
                b.Body.SetVelocity(away.normalized * 7f + Vector3.up * 3f + Vector3.back * 4f);
                GameAudio.PlayAt("dunk", b.transform.position, 1f, 1.3f);
                Effects.Burst(b.transform.position, new Color(0.8f, 0.4f, 1f), 16, 4f);
                Hud.Toast("SWATTED! Wait for his arms to come down!", new Color(0.8f, 0.6f, 1f), 1.2f);
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
                    transform.position += to.normalized * Mathf.Min(to.magnitude, 3.2f * dt);
                    FaceDir(to);
                }
            }
            else if (State == "idle" || State == "walk")
                FaceDir(PlayerPos - transform.position);

            Visual.rotation = Quaternion.RotateTowards(Visual.rotation, _face, 300f * dt);

            if (State != "defeated")
            {
                if (!Blocking) Base(_moving ? "Walk" : "Idle");
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
