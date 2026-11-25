using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Evaluates a list of input Signals. ALL must be on (or ANY, if set).
    /// </summary>
    [Serializable]
    public class SignalInputs
    {
        public List<Signal> Inputs = new List<Signal>();
        public bool Any;

        public bool Satisfied
        {
            get
            {
                if (Inputs.Count == 0) return true;
                if (Any)
                {
                    foreach (var s in Inputs) if (s != null && s.IsOn) return true;
                    return false;
                }
                foreach (var s in Inputs) if (s == null || !s.IsOn) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// Moves its transform between an OFF and ON local position depending on its
    /// inputs. Used for gates, cages, bridges and lifts. Tracks per-frame motion so
    /// the player can ride it.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SignalMover : MonoBehaviour
    {
        public SignalInputs Inputs = new SignalInputs();
        public bool Latch = true;
        public Vector3 OffLocal, OnLocal;
        public float Speed = 3f;
        public string OnSound = "gate";
        public event Action Activated;
        public bool Active { get; private set; }
        public Vector3 LastDelta { get; private set; }
        public bool IsLatched => _latched;
        bool _latched;

        void Update()
        {
            bool sat = Inputs.Satisfied;
            if (Latch && sat) _latched = true;
            bool want = Latch ? _latched : sat;
            if (want != Active)
            {
                Active = want;
                if (!string.IsNullOrEmpty(OnSound)) GameAudio.PlayAt(OnSound, transform.position, 0.8f, want ? 1f : 0.8f);
                if (want) Activated?.Invoke();
            }
            var before = transform.position;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, Active ? OnLocal : OffLocal,
                Speed * Time.deltaTime);
            LastDelta = transform.position - before;
        }

        public void ForceOpen()
        {
            _latched = true;
            Latch = true;
        }
    }

    /// <summary>
    /// A stone shrine gate. Its panel sinks into the floor when every input signal
    /// is on. Shows one indicator crystal per input above the doorway.
    /// </summary>
    public class Gate : MonoBehaviour
    {
        public SignalMover Panel { get; private set; }
        public bool IsOpen => Panel != null && Panel.Active;
        /// <summary>Open for good (latched) - this is what counts as "shrine solved".</summary>
        public bool IsSolved => IsOpen && Panel.IsLatched;
        /// <summary>Timed gates: once Zander walks through, the gate stays open.</summary>
        public bool LatchWhenPassed;
        readonly List<(Signal sig, Material mat)> _indicators = new List<(Signal, Material)>();

        public static Gate Create(string name, Transform parent, Vector3 localPos, float yaw, List<Signal> inputs,
            bool latch = true)
        {
            var root = Build.Group(name, parent, localPos, yaw);
            var gate = root.AddComponent<Gate>();
            gate.BuildVisuals(inputs, latch);
            return gate;
        }

        void BuildVisuals(List<Signal> inputs, bool latch)
        {
            var t = transform;
            var model = ModelLibrary.Spawn("Gate", t, Vector3.zero);
            Transform panelVis = null;
            if (model != null)
            {
                panelVis = ModelLibrary.FindDeep(model.transform, "GatePanel");
            }
            else
            {
                var stone = Mat.Color("#4A5162", 0.1f);
                Build.Box("PillarL", t, new Vector3(-1.75f, 2.15f, 0), new Vector3(0.5f, 4.3f, 0.7f), stone, false);
                Build.Box("PillarR", t, new Vector3(1.75f, 2.15f, 0), new Vector3(0.5f, 4.3f, 0.7f), stone, false);
                Build.Box("Lintel", t, new Vector3(0, 3.9f, 0), new Vector3(4f, 0.8f, 0.7f), stone, false);
            }
            // colliders for the frame
            Build.Collider("PillarLCol", t, new Vector3(-1.75f, 2.15f, 0), new Vector3(0.5f, 4.3f, 0.7f));
            Build.Collider("PillarRCol", t, new Vector3(1.75f, 2.15f, 0), new Vector3(0.5f, 4.3f, 0.7f));
            Build.Collider("LintelCol", t, new Vector3(0, 3.9f, 0), new Vector3(4f, 0.8f, 0.7f));

            // moving panel
            var panelRoot = new GameObject("PanelMover");
            panelRoot.transform.SetParent(t, false);
            if (panelVis != null) panelVis.SetParent(panelRoot.transform, true);
            else
            {
                Build.Box("Panel", panelRoot.transform, new Vector3(0, 1.75f, 0), new Vector3(3f, 3.5f, 0.25f),
                    Mat.Color("#2C3E66", 0.4f), false);
                Build.Prim(PrimitiveType.Cylinder, "Emblem", panelRoot.transform, new Vector3(0, 1.9f, -0.13f),
                    new Vector3(1.1f, 0.02f, 1.1f), Mat.Color("#E8772E"), false, Quaternion.Euler(90, 0, 0));
            }
            Build.Collider("PanelCol", panelRoot.transform, new Vector3(0, 1.75f, 0), new Vector3(3.05f, 3.5f, 0.3f));
            var rb = panelRoot.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            Panel = panelRoot.AddComponent<SignalMover>();
            Panel.Inputs.Inputs.AddRange(inputs);
            Panel.Latch = latch;
            Panel.OffLocal = Vector3.zero;
            Panel.OnLocal = new Vector3(0, -3.6f, 0);
            Panel.Speed = 2.5f;

            // indicator crystals, one per input, on both faces
            int n = inputs.Count;
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) / 2f) * 0.6f;
                var m = Mat.Glow(inputs[i].SignalColor * 0.3f, Color.black, 1);
                foreach (float z in new[] { -0.42f, 0.42f })
                    Build.Prim(PrimitiveType.Sphere, "Crystal" + i, t, new Vector3(x, 4.55f, z), Vector3.one * 0.32f, m, false);
                _indicators.Add((inputs[i], m));
            }
        }

        void Update()
        {
            if (LatchWhenPassed && IsOpen && !Panel.IsLatched && PlayerController.Instance != null)
            {
                var local = transform.InverseTransformPoint(PlayerController.Instance.transform.position);
                if (local.z > 0.9f && Mathf.Abs(local.x) < 2.5f)
                {
                    Panel.ForceOpen();
                    GameAudio.Play("chime", 0.6f);
                }
            }
            foreach (var (sig, mat) in _indicators)
            {
                if (sig == null) continue;
                bool on = sig.IsOn || IsOpen;
                var c = sig.SignalColor;
                mat.color = on ? c : c * 0.3f;
                Mat.SetEmission(mat, on ? c * 2.5f : Color.black);
            }
        }
    }

    /// <summary>A lift that ping-pongs between two points once powered.</summary>
    [DefaultExecutionOrder(-50)]
    public class MovingPlatform : MonoBehaviour
    {
        public SignalInputs Inputs = new SignalInputs();
        public Vector3 A, B;              // local positions
        public float Speed = 2f;
        public float Pause = 1.5f;
        public bool LatchPower = true;
        public Vector3 LastDelta { get; private set; }
        public bool Powered { get; private set; }
        Material _glow;
        bool _toB = true;
        float _wait;

        public void SetGlow(Material m) => _glow = m;

        void Update()
        {
            bool sat = Inputs.Satisfied;
            if (sat && !Powered) GameAudio.PlayAt("gate", transform.position, 0.6f, 1.4f);
            Powered = LatchPower ? (Powered || sat) : sat;
            if (_glow != null) Mat.SetEmission(_glow, Powered ? new Color(0.2f, 1f, 0.5f) * 1.5f : Color.black);
            var before = transform.position;
            if (Powered)
            {
                if (_wait > 0) _wait -= Time.deltaTime;
                else
                {
                    var target = _toB ? B : A;
                    transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, Speed * Time.deltaTime);
                    if ((transform.localPosition - target).sqrMagnitude < 0.0001f)
                    {
                        _toB = !_toB;
                        _wait = Pause;
                    }
                }
            }
            LastDelta = transform.position - before;
        }
    }

    /// <summary>Launches Zander high into the air (and bounces balls).</summary>
    public class BouncePad : MonoBehaviour
    {
        public SignalInputs Inputs = new SignalInputs();
        public float LaunchHeight = 6f;
        public bool Powered => Inputs.Satisfied;
        Transform _top;
        Vector3 _topRest;
        Material _glow;
        float _squash;

        public static BouncePad Create(string name, Transform parent, Vector3 localPos, float height)
        {
            var root = Build.Group(name, parent, localPos);
            var pad = root.AddComponent<BouncePad>();
            pad.LaunchHeight = height;
            var model = ModelLibrary.Spawn("BouncePad", root.transform, Vector3.zero);
            pad._glow = Mat.Glow(Mat.Hex("#3CE07A"), Mat.Hex("#1FB85A"), 1f);
            if (model != null)
            {
                pad._top = ModelLibrary.FindDeep(model.transform, "PadTop");
                if (pad._top != null)
                    foreach (var r in pad._top.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] != null && mats[i].name.Contains("PadTop")) mats[i] = pad._glow;
                        r.sharedMaterials = mats;
                    }
            }
            else
            {
                Build.Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0, 0.1f, 0), new Vector3(1.7f, 0.1f, 1.7f),
                    Mat.Color("#373D4B"), false);
                pad._top = Build.Prim(PrimitiveType.Cylinder, "PadTop", root.transform, new Vector3(0, 0.24f, 0),
                    new Vector3(1.4f, 0.04f, 1.4f), pad._glow, false).transform;
            }
            if (pad._top != null) pad._topRest = pad._top.localPosition;
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = 0.85f;
            col.height = 0.28f;
            col.center = new Vector3(0, 0.14f, 0);
            Build.Label("BOUNCE!", root.transform, new Vector3(0, 0.9f, 0), 0.35f, Mat.Hex("#3CE07A"), 0, true);
            return pad;
        }

        public void Launch(PlayerController p)
        {
            if (!Powered) return;
            float v = Mathf.Sqrt(2f * PlayerController.Gravity * LaunchHeight);
            p.Launch(v);
            _squash = 1f;
            GameAudio.PlayAt("boing", transform.position, 1f);
            Rumble.Pulse(0.4f, 0.3f, 0.18f);
            Effects.Burst(transform.position + Vector3.up * 0.3f, Mat.Hex("#3CE07A"), 16, 4f);
        }

        void Update()
        {
            _squash = Mathf.MoveTowards(_squash, 0, Time.deltaTime * 4f);
            if (_top != null) _top.localPosition = _topRest + Vector3.down * 0.08f * _squash;
            bool on = Powered;
            Mat.SetEmission(_glow, on ? Mat.Hex("#1FB85A") * (1.2f + 0.5f * Mathf.Sin(Time.time * 4)) : Color.black);
            _glow.color = on ? Mat.Hex("#3CE07A") : Mat.Hex("#4C5566");
        }

        void OnCollisionEnter(Collision c)
        {
            var b = c.rigidbody != null ? c.rigidbody.GetComponent<Basketball>() : null;
            if (b != null && Powered)
            {
                var v = b.Body.GetVelocity();
                v.y = Mathf.Max(v.y, 9f);
                b.Body.SetVelocity(v);
                GameAudio.PlayAt("boing", transform.position, 0.5f, 1.4f);
            }
        }
    }

    /// <summary>Keeps up to MaxActive balls in play, re-spawning them on its cup.</summary>
    public class BallDispenser : MonoBehaviour
    {
        public static readonly List<BallDispenser> All = new List<BallDispenser>();
        public int MaxActive = 1;
        public float RespawnDelay = 1.0f;
        public Vector3 SpawnLocal = new Vector3(0, 1.08f, 0);
        readonly List<Basketball> _active = new List<Basketball>();
        float _timer;
        public Vector3 SpawnPoint => transform.TransformPoint(SpawnLocal);

        public static BallDispenser Create(string name, Transform parent, Vector3 localPos, int maxActive)
        {
            var root = Build.Group(name, parent, localPos);
            var d = root.AddComponent<BallDispenser>();
            d.MaxActive = maxActive;
            var model = ModelLibrary.Spawn("BallDispenser", root.transform, Vector3.zero);
            if (model == null)
            {
                Build.Prim(PrimitiveType.Cylinder, "Column", root.transform, new Vector3(0, 0.45f, 0),
                    new Vector3(0.55f, 0.45f, 0.55f), Mat.Color("#565D6B"), false);
                Build.Prim(PrimitiveType.Cylinder, "Top", root.transform, new Vector3(0, 0.92f, 0),
                    new Vector3(0.66f, 0.04f, 0.66f), Mat.Color("#2C3E66"), false);
            }
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = 0.33f;
            col.height = 0.95f;
            col.center = new Vector3(0, 0.475f, 0);
            Build.PointLight(root.transform, new Vector3(0, 1.4f, 0), Mat.Hex("#FFB347"), 3f, 1.2f);
            return d;
        }

        void Awake() => All.Add(this);
        void OnDestroy() => All.Remove(this);

        void Update()
        {
            _active.RemoveAll(b => b == null);
            if (_active.Count >= MaxActive) return;
            bool slotBusy = false;
            foreach (var b in Basketball.All)
                if (b != null && !b.IsHeld && (b.transform.position - SpawnPoint).sqrMagnitude < 0.25f)
                    slotBusy = true;
            if (slotBusy) return;
            _timer += Time.deltaTime;
            if (_timer < RespawnDelay) return;
            _timer = 0;
            Spawn();
        }

        public Basketball Spawn()
        {
            var b = Basketball.Create(SpawnPoint);
            b.Source = this;
            b.Dock(SpawnPoint);
            _active.Add(b);
            Effects.Burst(SpawnPoint, Mat.Hex("#FFB347"), 10, 1.5f, 0.1f, 0.5f, false);
            return b;
        }

        public void ResetBalls()
        {
            foreach (var b in _active)
                if (b != null && !b.IsHeld) Destroy(b.gameObject);
            _active.RemoveAll(b => b == null);
            _timer = RespawnDelay;
        }
    }

    /// <summary>Something Zander can use with the Interact key (E).</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();
        public float Range = 2.6f;
        public abstract string Prompt { get; }
        public abstract void Interact(PlayerController player);
        public virtual Vector3 Point => transform.position;
        protected virtual void Awake() => All.Add(this);
        protected virtual void OnDestroy() => All.Remove(this);
    }

    /// <summary>
    /// A rotating "Rebound Board". When a ball hits it, the board magically fires the
    /// ball in the direction its arrow points: into a hoop or at another board.
    /// Press E next to it to turn it 45 degrees. A laser shows where it is aiming.
    /// </summary>
    public class ReboundBoard : Interactable
    {
        public static readonly List<ReboundBoard> AllBoards = new List<ReboundBoard>();
        public bool Aimable = true;   // can be targeted by the player's aim assist
        public bool Rotatable = true;
        public float ConeDegrees = 28f;
        public string DisplayName = "Rebound Board";
        public const float BoardCenterHeight = 2.0f;

        Transform _pivot;
        Transform _face;
        float _targetYaw;
        Component _lockedOn;
        LineRenderer _laser;
        Material _laserMat;
        float _cooldown;

        public Vector3 FaceCenter => _face.position;
        public Vector3 Facing
        {
            get
            {
                var f = _pivot.forward;
                f.y = 0;
                return f.normalized;
            }
        }

        public static ReboundBoard Create(string name, Transform parent, Vector3 localPos, float yaw, bool aimable = true,
            bool rotatable = true)
        {
            var root = Build.Group(name, parent, localPos);
            var rb = root.AddComponent<ReboundBoard>();
            rb.DisplayName = name;
            rb.Aimable = aimable;
            rb.Rotatable = rotatable;
            rb.BuildVisuals(yaw);
            return rb;
        }

        protected override void Awake()
        {
            base.Awake();
            AllBoards.Add(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            AllBoards.Remove(this);
        }

        void BuildVisuals(float yaw)
        {
            var t = transform;
            var model = ModelLibrary.Spawn("ReboundBoard", t, Vector3.zero);
            ModelLibrary.FaceByBounds(model, 1f);   // the arrow on top points the way the board fires (+Z)
            Transform pivotVis = model != null ? ModelLibrary.FindDeep(model.transform, "BoardPivot") : null;

            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(t, false);
            _pivot = pivot.transform;
            if (pivotVis != null) pivotVis.SetParent(_pivot, true);
            else
            {
                if (model == null)
                    Build.Prim(PrimitiveType.Cylinder, "Base", t, new Vector3(0, 0.1f, 0), new Vector3(1.1f, 0.1f, 1.1f),
                        Mat.Color("#373D4B"), false);
                Build.Prim(PrimitiveType.Cylinder, "Post", _pivot, new Vector3(0, 0.75f, 0), new Vector3(0.16f, 0.55f, 0.16f),
                    Mat.Color("#8C96A6", 0.6f, 0.6f), false);
                Build.Box("Panel", _pivot, new Vector3(0, BoardCenterHeight, 0), new Vector3(1.6f, 1.6f, 0.12f),
                    Mat.Color("#DDEBFF", 0.8f), false);
                Build.Box("Arrow", _pivot, new Vector3(0, 2.9f, 0.3f), new Vector3(0.12f, 0.08f, 0.7f), Mat.Color("#FFC21A"), false);
            }
            var face = new GameObject("FaceCenter");
            face.transform.SetParent(_pivot, false);
            face.transform.localPosition = new Vector3(0, BoardCenterHeight, 0.1f);
            _face = face.transform;
            var col = Build.Collider("BoardCol", _pivot, new Vector3(0, BoardCenterHeight, 0), new Vector3(1.6f, 1.6f, 0.16f));
            col.GetComponent<BoxCollider>().sharedMaterial = Basketball.BallPhysics;
            Build.Collider("PostCol", t, new Vector3(0, 0.6f, 0), new Vector3(0.4f, 1.2f, 0.4f));

            _targetYaw = yaw;
            _pivot.localRotation = Quaternion.Euler(0, yaw, 0);

            _laserMat = Mat.Line(new Color(0.3f, 0.9f, 1f, 0.8f));
            _laser = pivot.AddComponent<LineRenderer>();
            _laser.sharedMaterial = _laserMat;
            _laser.positionCount = 2;
            _laser.widthMultiplier = 0.05f;
            _laser.useWorldSpace = true;
            _laser.numCapVertices = 2;
        }

        public override Vector3 Point => transform.position;
        public override string Prompt => Rotatable ? "Press {USE} to turn the Rebound Board" : null;

        public const float TargetRange = 18f;

        static float Bearing(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

        /// <summary>A board can only feed a hoop from in front of (or beside) it - never through the backboard.</summary>
        static bool CanFeed(Hoop h, Vector3 from)
        {
            var toBoard = from - h.RimCenter;
            toBoard.y = 0;
            var fwd = h.transform.forward;
            fwd.y = 0;
            return toBoard.sqrMagnitude < 0.01f || Vector3.Dot(fwd.normalized, toBoard.normalized) > -0.2f;
        }

        /// <summary>
        /// Each press swings the board to point EXACTLY at the next target clockwise
        /// (a hoop or another board within range), so the laser always locks on.
        /// </summary>
        public override void Interact(PlayerController player)
        {
            if (!Rotatable) return;
            var bearings = new List<float>();
            var here = transform.position;
            foreach (var h in Hoop.All)
            {
                if (h == null || h.DunkOnly || h.Mode == HoopMode.Counter || !CanFeed(h, here)) continue;
                var d = h.RimCenter - here;
                d.y = 0;
                if (d.magnitude > 1f && d.magnitude < TargetRange) bearings.Add(Bearing(d));
            }
            foreach (var b in AllBoards)
            {
                if (b == null || b == this) continue;
                var d = b.transform.position - here;
                d.y = 0;
                if (d.magnitude > 1f && d.magnitude < TargetRange) bearings.Add(Bearing(d));
            }
            float step = 45f;
            if (bearings.Count > 0)
            {
                float cur = Mathf.Repeat(_targetYaw, 360f);
                step = float.MaxValue;
                foreach (float y in bearings)
                {
                    float delta = Mathf.Repeat(y - cur, 360f);
                    if (delta < 3f) delta += 360f;      // already pointing at this one: go to the next
                    step = Mathf.Min(step, delta);
                }
            }
            _targetYaw += step;
            GameAudio.PlayAt("turn", transform.position, 0.9f);
            Rumble.Pulse(0f, 0.3f, 0.08f);
        }

        /// <summary>The thing this board would fire at (a hoop or another board), or null.</summary>
        public Component FindTarget(out Vector3 point)
        {
            point = FaceCenter + Facing * 12f;
            Component best = null;
            float bestAngle = ConeDegrees;
            var from = FaceCenter;
            foreach (var h in Hoop.All)
            {
                if (h == null || h.DunkOnly || h.Mode == HoopMode.Counter || !CanFeed(h, from)) continue;
                var d = h.RimCenter - from;
                d.y = 0;
                if (d.magnitude < 0.5f) continue;
                float a = Vector3.Angle(Facing, d);
                if (a < bestAngle)
                {
                    bestAngle = a;
                    best = h;
                    point = h.RimCenter;
                }
            }
            foreach (var b in AllBoards)
            {
                if (b == this || b == null) continue;
                var d = b.FaceCenter - from;
                d.y = 0;
                float a = Vector3.Angle(Facing, d);
                if (a < bestAngle)
                {
                    bestAngle = a;
                    best = b;
                    point = b.FaceCenter;
                }
            }
            return best;
        }

        /// <summary>
        /// Catch zone in front of the board: a ball inside it that is travelling toward the
        /// face gets redirected, even if it would only have clipped the edge.
        /// </summary>
        public bool TryCatch(Basketball ball, Vector3 pos)
        {
            if (Time.time < _cooldown || ball.IsHeld || ball.IsDocked) return false;
            var local = _pivot.InverseTransformPoint(pos);
            if (Mathf.Abs(local.x) > 1.0f || Mathf.Abs(local.y - BoardCenterHeight) > 1.0f) return false;
            if (local.z < -0.1f || local.z > 0.75f) return false;
            if (Vector3.Dot(ball.Body.GetVelocity(), Facing) >= -0.5f) return false;   // must be coming at the face
            if (FindTarget(out _) == null) return false;   // not lined up: let it bounce off normally
            OnBallHit(ball);
            return true;
        }

        public void OnBallHit(Basketball ball)
        {
            if (Time.time < _cooldown || ball.IsHeld) return;
            // only balls hitting the FRONT (the side the arrow/laser points) get fired off;
            // anything hitting the back or sides just bounces off normally
            var rel = ball.transform.position - FaceCenter;
            rel.y = 0;
            if (Vector3.Dot(rel, Facing) < -0.05f) return;
            var target = FindTarget(out var point);
            if (target == null) return; // plain physics bounce
            _cooldown = Time.time + 0.3f;
            var start = FaceCenter + Facing * 0.45f;
            Vector3 v;
            if (target is Hoop hoop)
                v = Ballistics.SolveLeading(start, point, hoop.Velocity, Ballistics.ShotArc(start, point) + 0.5f, out _);
            else
                v = Ballistics.Solve(start, point + Vector3.up * 0.1f, 1.2f, out _);
            ball.Redirect(start, v);
            GameAudio.PlayAt("turn", transform.position, 1f, 1.6f);
            Effects.Burst(start, new Color(0.3f, 0.9f, 1f), 14, 3f, 0.1f, 0.5f, false);
        }

        void Update()
        {
            _pivot.localRotation = Quaternion.RotateTowards(_pivot.localRotation, Quaternion.Euler(0, _targetYaw, 0),
                220f * Time.deltaTime);
            var target = FindTarget(out var point);
            bool settled = Quaternion.Angle(_pivot.localRotation, Quaternion.Euler(0, _targetYaw, 0)) < 1f;
            if (settled && target != null && target != _lockedOn)
            {
                GameAudio.PlayAt("chime", transform.position, 0.6f, 1.5f);   // lock-on!
                Effects.Burst(point, new Color(0.35f, 1f, 0.5f), 12, 2f, 0.1f, 0.5f, false);
            }
            if (settled) _lockedOn = target;
            _laser.SetPosition(0, FaceCenter);
            _laser.SetPosition(1, target != null ? point : FaceCenter + Facing * 6f);
            var c = target != null ? new Color(0.35f, 1f, 0.5f, 0.9f) : new Color(1f, 0.35f, 0.3f, 0.6f);
            _laserMat.color = c;
            _laser.widthMultiplier = target != null ? 0.1f + 0.03f * Mathf.Sin(Time.time * 8) : 0.05f;
        }
    }

    /// <summary>
    /// Requires hoops to be scored in a specific order. A wrong hoop resets the
    /// sequence. Is ON when the whole sequence has been completed.
    /// </summary>
    public class HoopSequence : Signal
    {
        public List<Hoop> Order = new List<Hoop>();
        public int Progress { get; private set; }
        bool _done;
        readonly List<Material> _orbs = new List<Material>();

        public override bool IsOn => _done;
        public override Color SignalColor => new Color(1f, 0.85f, 0.2f);

        public void Setup(List<Hoop> order, Transform displayParent, Vector3 displayPos, float yaw)
        {
            Order = order;
            foreach (var h in order) h.Scored += OnScored;
            // A totem showing the order (1st at the top)
            var totem = Build.Group("SequenceTotem", displayParent, displayPos, yaw);
            Build.StoneBox("Column", totem.transform, new Vector3(0, 2.2f, 0), new Vector3(1.2f, 4.4f, 1.2f));
            for (int i = 0; i < order.Count; i++)
            {
                var c = order[i].HoopColor;
                var m = Mat.Glow(c * 0.35f, c * 0.2f, 1f);
                _orbs.Add(m);
                float y = 3.8f - i * 1.15f;
                foreach (float z in new[] { -0.62f, 0.62f })
                {
                    Build.Prim(PrimitiveType.Sphere, "Orb" + i, totem.transform, new Vector3(0, y, z), Vector3.one * 0.55f, m, false);
                    Build.Label((i + 1).ToString(), totem.transform, new Vector3(0.48f, y, z * 1.02f), 0.5f, Color.white,
                        z < 0 ? 0 : 180);
                }
            }
            Build.Label("ORDER", totem.transform, new Vector3(0, 4.9f, 0), 0.5f, Color.white, 0, true);
        }

        void OnScored(Hoop h, Basketball b)
        {
            if (_done) return;
            int idx = Order.IndexOf(h);
            if (idx < Progress) return; // already done - ignore repeats
            if (idx == Progress)
            {
                Progress++;
                if (Progress >= Order.Count)
                {
                    _done = true;
                    GameAudio.Play("solve");
                }
            }
            else
            {
                Hud.Toast("Wrong order! Look at the ORDER totem and try again.", Color.white);
                GameAudio.Play("buzz", 0.8f);
                Progress = 0;
                foreach (var o in Order) o.ResetLight();
            }
        }

        void Update()
        {
            for (int i = 0; i < _orbs.Count; i++)
            {
                var c = Order[i].HoopColor;
                bool on = i < Progress;
                bool next = i == Progress && !_done;
                float pulse = next ? 0.6f + 0.4f * Mathf.Sin(Time.time * 5f) : 0f;
                _orbs[i].color = on ? c : c * (0.35f + 0.3f * pulse);
                Mat.SetEmission(_orbs[i], on ? c * 2.5f : c * (0.15f + pulse));
            }
        }
    }

    /// <summary>
    /// A blue plate that, once stood on for HoldSeconds, turns ON for OnSeconds
    /// (a countdown shows above it). Used to teach dashing and stamina.
    /// </summary>
    public class TimedSwitch : Signal
    {
        public PressurePlate Plate;
        public float HoldSeconds;
        public float OnSeconds = 3f;
        float _held;
        float _onUntil;
        float _lastTick;
        TextMesh _label;
        Material _ring;

        public override bool IsOn => Time.time < _onUntil;
        public override Color SignalColor => new Color(0.3f, 0.75f, 1f);
        public float TimeLeft => Mathf.Max(0f, _onUntil - Time.time);

        public static TimedSwitch Create(string name, Transform parent, Vector3 localPos, float holdSeconds, float onSeconds)
        {
            var plate = PressurePlate.Create(name, parent, localPos, PlateKind.Feet);
            var ts = plate.gameObject.AddComponent<TimedSwitch>();
            ts.Plate = plate;
            ts.HoldSeconds = holdSeconds;
            ts.OnSeconds = onSeconds;
            ts._label = Build.Label("", plate.transform, new Vector3(0, 2.2f, 0), 0.55f, Color.white, 0, true);
            ts._ring = Mat.Glow(new Color(0.3f, 0.75f, 1f), new Color(0.3f, 0.75f, 1f), 1.5f);
            FloorRing(plate.transform.parent, localPos + Vector3.up * 0.02f, 1.25f, ts._ring, 0f, 360f, 32);
            return ts;
        }

        void Update()
        {
            bool standing = Plate != null && Plate.Pressed;
            if (!IsOn)
            {
                if (standing)
                {
                    _held += Time.deltaTime;
                    if (_held >= HoldSeconds)
                    {
                        _held = 0f;
                        _onUntil = Time.time + OnSeconds;
                        _lastTick = Time.time;
                        GameAudio.Play("whistle", 0.6f);
                        Hud.Toast("GO GO GO!", new Color(0.4f, 0.85f, 1f), 0.8f);
                    }
                }
                else _held = 0f;
            }
            else if (Time.time - _lastTick >= 1f)
            {
                _lastTick = Time.time;
                GameAudio.Play("tick", 0.7f, 1.2f);
            }
            if (_label != null)
            {
                if (IsOn) _label.text = TimeLeft.ToString("0.0");
                else if (_held > 0f) _label.text = "HOLD " + Mathf.Max(0f, HoldSeconds - _held).ToString("0.0");
                else _label.text = HoldSeconds > 0f ? "STAND " + HoldSeconds.ToString("0.#") + "s" : "STEP";
                _label.color = IsOn ? new Color(0.4f, 1f, 0.5f) : Color.white;
            }
            if (_ring != null)
                Mat.SetEmission(_ring, IsOn ? new Color(0.4f, 1f, 0.5f) * 2f : new Color(0.3f, 0.75f, 1f) * (standing ? 2f : 0.8f));
        }

        /// <summary>A glowing ring (or arc) painted on the floor out of short segments.</summary>
        public static void FloorRing(Transform parent, Vector3 center, float radius, Material mat, float fromDeg, float toDeg,
            int segments)
        {
            float span = (toDeg - fromDeg) * Mathf.Deg2Rad;
            float segLen = Mathf.Abs(span) * radius / segments + 0.05f;
            for (int i = 0; i < segments; i++)
            {
                float a = fromDeg * Mathf.Deg2Rad + span * (i + 0.5f) / segments;
                var p = center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                Build.Box("Ring", parent, p, new Vector3(0.12f, 0.02f, segLen), mat, false,
                    Quaternion.Euler(0, a * Mathf.Rad2Deg + 90f, 0));
            }
        }
    }

    /// <summary>Anyone Zander can steal the ball from (King Swat, sparring bots).</summary>
    public abstract class BallCarrier : MonoBehaviour
    {
        public static readonly List<BallCarrier> All = new List<BallCarrier>();
        public abstract Basketball HeldBall { get; }
        public abstract Basketball LoseBall();
        /// <summary>Chance that one of Zander's steal attempts works.</summary>
        public virtual float StealChance => 0.55f;
        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);
    }

    /// <summary>
    /// A friendly practice robot for the Steal shrine. It dribbles its own ball around
    /// a patrol loop. Options: flee from Zander (catch it with a dash), and chase Zander
    /// to steal the ball back (dodge it).
    /// </summary>
    public class SparringBot : BallCarrier
    {
        public Vector3 Center;
        public float Radius = 3f;
        public float PatrolSpeed = 2f;
        public float FleeSpeed;        // > 0: runs away when Zander is close
        public float ChaseSpeed;       // > 0: chases Zander to steal the ball back
        public float LungeChance = 0.35f;
        public float Chance = 0.9f;
        public Hoop Target;            // once this hoop is lit, the bot stops playing
        public float MinX, MaxX, MinZ, MaxZ;

        Basketball _held, _myBall;
        Color _color = Color.white;
        public Basketball MyBall => _myBall;
        public float FleeRadius = 6f;
        float _ang, _sadUntil, _nextLunge, _dribbleT, _t;
        bool _lunging;
        Transform _body, _armL, _armR;
        Vector3 _face = Vector3.forward;

        public override Basketball HeldBall => _held;
        public override float StealChance => Chance;
        bool Done => Target != null && Target.Lit;

        public static SparringBot Create(string name, Transform parent, Vector3 center, float radius, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center + new Vector3(radius, 0, 0);
            var bot = go.AddComponent<SparringBot>();
            bot.Center = parent.TransformPoint(center);
            bot.Radius = radius;
            bot._color = color;
            bot.BuildVisual(color, name.ToUpperInvariant());
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.4f;
            col.height = 1.6f;
            col.center = new Vector3(0, 0.8f, 0);
            go.AddComponent<Rigidbody>().isKinematic = true;
            return bot;
        }

        void BuildVisual(Color color, string label)
        {
            var root = new GameObject("Visual").transform;
            root.SetParent(transform, false);
            _body = root;
            var main = Mat.Color(color, 0.4f);
            var white = Mat.Color("#F4F4F4", 0.4f);
            var dark = Mat.Color("#1E1E24", 0.3f);
            var eye = Mat.Glow(Mat.Hex("#4AD7FF"), Mat.Hex("#29C4FF"), 2.5f);
            Build.Prim(PrimitiveType.Sphere, "Base", root, new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.35f, 0.7f), dark, false);
            Build.Prim(PrimitiveType.Capsule, "Body", root, new Vector3(0, 0.8f, 0), new Vector3(0.75f, 0.45f, 0.65f), main, false);
            Build.Prim(PrimitiveType.Cylinder, "Belt", root, new Vector3(0, 0.62f, 0), new Vector3(0.78f, 0.05f, 0.68f), white, false);
            Build.Prim(PrimitiveType.Sphere, "Head", root, new Vector3(0, 1.38f, 0), new Vector3(0.62f, 0.55f, 0.6f), white, false);
            Build.Prim(PrimitiveType.Cube, "Visor", root, new Vector3(0, 1.4f, 0.24f), new Vector3(0.44f, 0.16f, 0.12f), dark, false);
            Build.Prim(PrimitiveType.Cube, "Eyes", root, new Vector3(0, 1.4f, 0.3f), new Vector3(0.34f, 0.06f, 0.02f), eye, false);
            Build.Prim(PrimitiveType.Cylinder, "Antenna", root, new Vector3(0, 1.75f, 0), new Vector3(0.03f, 0.12f, 0.03f), dark, false);
            Build.Prim(PrimitiveType.Sphere, "Tip", root, new Vector3(0, 1.9f, 0), Vector3.one * 0.1f, eye, false);
            _armL = new GameObject("ArmL").transform;
            _armL.SetParent(root, false);
            _armL.localPosition = new Vector3(0.42f, 1.0f, 0);
            _armR = new GameObject("ArmR").transform;
            _armR.SetParent(root, false);
            _armR.localPosition = new Vector3(-0.42f, 1.0f, 0);
            foreach (var arm in new[] { _armL, _armR })
            {
                Build.Prim(PrimitiveType.Capsule, "Arm", arm, new Vector3(0, -0.22f, 0), new Vector3(0.16f, 0.22f, 0.16f), main, false);
                Build.Prim(PrimitiveType.Sphere, "Hand", arm, new Vector3(0, -0.48f, 0), Vector3.one * 0.18f, white, false);
            }
            Build.Label(label, transform, new Vector3(0, 2.3f, 0), 0.3f, color, 0, true);
        }

        Vector3 PlayerPos => PlayerController.Instance != null ? PlayerController.Instance.transform.position : transform.position;
        bool PlayerHasBall => PlayerController.Instance != null && PlayerController.Instance.Balls != null &&
                              PlayerController.Instance.Balls.HasBall;
        /// <summary>Zander is carrying THIS bot's ball: it wants it back.</summary>
        bool PlayerHasMyBall => PlayerHasBall && _myBall != null && PlayerController.Instance.Balls.Ball == _myBall;

        public override Basketball LoseBall()
        {
            var b = _held;
            _held = null;
            _sadUntil = Time.time + 1.2f;
            _nextLunge = Time.time + 2.0f;
            GameAudio.PlayAt("buzz", transform.position, 0.4f, 1.6f);
            return b;
        }

        void MoveToward(Vector3 target, float speed)
        {
            var d = target - transform.position;
            d.y = 0;
            if (d.magnitude < 0.05f) return;
            var step = d.normalized * Mathf.Min(d.magnitude, speed * Time.deltaTime);
            var p = transform.position + step;
            p.x = Mathf.Clamp(p.x, MinX, MaxX);
            p.z = Mathf.Clamp(p.z, MinZ, MaxZ);
            transform.position = p;
            _face = d.normalized;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;
            if (_myBall == null)
            {
                _myBall = Basketball.Create(transform.position + Vector3.up);
                _myBall.SetBand(_color);
                _held = _myBall;
                _myBall.Hold(this);
            }
            float moved = 0f;
            var before = transform.position;
            if (_lunging) { }
            else if (_held != null)
            {
                float pd = Vector3.Distance(new Vector3(PlayerPos.x, 0, PlayerPos.z), new Vector3(transform.position.x, 0, transform.position.z));
                if (FleeSpeed > 0f && pd < FleeRadius)
                {
                    var away = transform.position - PlayerPos;
                    away.y = 0;
                    // run away, sliding along the walls instead of getting stuck in a corner
                    var target = transform.position + away.normalized * 3f + Vector3.Cross(Vector3.up, away.normalized) * 1.5f;
                    MoveToward(target, FleeSpeed);
                }
                else
                {
                    _ang += PatrolSpeed / Mathf.Max(0.5f, Radius) * dt;
                    MoveToward(Center + new Vector3(Mathf.Cos(_ang), 0, Mathf.Sin(_ang)) * Radius, PatrolSpeed * 1.5f);
                }
            }
            else if (Time.time < _sadUntil || Done) { _face = PlayerPos - transform.position; }
            else if (ChaseSpeed > 0f && PlayerHasMyBall)
            {
                MoveToward(PlayerPos, ChaseSpeed);
                if (Time.time > _nextLunge && Vector3.Distance(PlayerPos, transform.position) < 2.2f)
                    StartCoroutine(Lunge());
            }
            else if (_myBall != null && !_myBall.IsHeld)
            {
                // go get our ball back
                var bp = _myBall.transform.position;
                MoveToward(bp, Mathf.Max(PatrolSpeed, 3f));
                var flat = new Vector2(bp.x - transform.position.x, bp.z - transform.position.z).magnitude;
                if (flat < 0.8f && bp.y < 1.6f)
                {
                    _held = _myBall;
                    _myBall.Hold(this);
                }
            }
            moved = (transform.position - before).magnitude / Mathf.Max(dt, 0.0001f);

            // waddle animation
            _face.y = 0;
            if (_face.sqrMagnitude > 0.001f)
                _body.rotation = Quaternion.RotateTowards(_body.rotation, Quaternion.LookRotation(_face), 540f * dt);
            float sw = Mathf.Sin(_t * 10f) * Mathf.Clamp01(moved / 3f);
            _body.localPosition = new Vector3(0, Mathf.Abs(sw) * 0.06f, 0);
            _armL.localRotation = Quaternion.Euler(-40f * sw, 0, 0);
            _armR.localRotation = _held != null
                ? Quaternion.Euler(-35f - 25f * Mathf.Abs(Mathf.Cos(_dribbleT * Mathf.PI * 2f)), 0, 0)
                : Quaternion.Euler(40f * sw, 0, 0);
            if (Done && _held == null)
                _armL.localRotation = _armR.localRotation = Quaternion.Euler(-160f + 20f * Mathf.Sin(_t * 8f), 0, 0); // cheering
        }

        IEnumerator Lunge()
        {
            _lunging = true;
            Hud.Toast("The bot is going for the ball - DASH away!", new Color(1f, 0.7f, 0.4f), 0.9f);
            _armR.localRotation = Quaternion.Euler(-80f, 0, 0);
            yield return new WaitForSeconds(0.4f);
            var player = PlayerController.Instance;
            if (PlayerHasMyBall && Vector3.Distance(PlayerPos, transform.position) < 2.5f && UnityEngine.Random.value < LungeChance)
            {
                var b = player.Balls.Ball;
                player.Balls.ForgetBall();
                _held = b;
                _myBall = b;
                b.Hold(this);
                GameAudio.Play("dunk", 0.6f, 1.6f);
                Hud.Toast("The bot stole it back! Steal it again ({USE})", new Color(1f, 0.55f, 0.5f), 1.4f);
            }
            else if (PlayerHasBall) Hud.Toast("Nice dodge!", new Color(0.5f, 1f, 0.6f), 0.8f);
            _nextLunge = Time.time + 3f;
            _lunging = false;
        }

        void LateUpdate()
        {
            if (_held == null) return;
            _dribbleT += Time.deltaTime;
            var fwd = _body.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var pos = transform.position + right * 0.45f + fwd * 0.35f;
            float k = Mathf.Abs(Mathf.Cos(Mathf.PI * _dribbleT / 0.45f));
            pos.y = transform.position.y + Basketball.Radius + (0.85f - Basketball.Radius) * k;
            _held.transform.position = pos;
        }
    }
}
