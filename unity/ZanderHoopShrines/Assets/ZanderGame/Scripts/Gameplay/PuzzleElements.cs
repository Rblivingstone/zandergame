using System;
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
        public override string Prompt => Rotatable ? "Press E to turn the Rebound Board" : null;

        public override void Interact(PlayerController player)
        {
            if (!Rotatable) return;
            _targetYaw += 45f;
            GameAudio.PlayAt("turn", transform.position, 0.9f);
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
                if (h == null || h.DunkOnly || h.Mode == HoopMode.Counter) continue;
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

        public void OnBallHit(Basketball ball)
        {
            if (Time.time < _cooldown || ball.IsHeld) return;
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
            _laser.SetPosition(0, FaceCenter);
            _laser.SetPosition(1, target != null ? point : FaceCenter + Facing * 6f);
            var c = target != null ? new Color(0.35f, 1f, 0.5f, 0.9f) : new Color(1f, 0.35f, 0.3f, 0.6f);
            _laserMat.color = c;
            _laser.widthMultiplier = target != null ? 0.07f + 0.02f * Mathf.Sin(Time.time * 8) : 0.04f;
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
}
