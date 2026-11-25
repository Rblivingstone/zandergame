using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    public enum HoopMode
    {
        Latch,   // stays lit forever once scored (until ResetLight)
        Timed,   // lit for LitDuration seconds
        Counter  // just reports scores (boss contest)
    }

    /// <summary>
    /// A basketball hoop. Origin = floor under the rim; the hoop faces local +Z
    /// (rim in front, backboard behind). Rim centre is RimHeight above the origin.
    /// </summary>
    public class Hoop : Signal
    {
        public static readonly List<Hoop> All = new List<Hoop>();
        public const float RimHeight = 3.0f;
        public const float DefaultRimRadius = 0.32f;

        public HoopMode Mode = HoopMode.Latch;
        public float LitDuration = 10f;
        public bool DunkOnly;
        public bool RequiresRebound;
        public bool AimAssist = true;
        public string DisplayName = "Hoop";
        public Color HoopColor = new Color(1f, 0.45f, 0.1f);
        public float RimRadius = DefaultRimRadius;
        public bool AcceptPlayerShots = true;
        /// <summary>Only shots taken from at least this far away count (0 = any).</summary>
        public float MinShotDistance;
        /// <summary>Lights up only after Zander hangs on the rim for HangSeconds.</summary>
        public bool LightByHang;
        /// <summary>Steal shrine: only this sparring bot's ball counts in this hoop.</summary>
        public SparringBot RequiredOwner;
        public float HangSeconds = 3f;
        float _hang;

        public event Action<Hoop, Basketball> Scored;

        public Transform Rim { get; private set; }
        public Vector3 RimCenter => Rim.position;
        public Vector3 Velocity { get; private set; }
        public bool Lit => _lit && (Mode != HoopMode.Timed || Time.time < _litUntil);
        public float TimeLeft => Mode == HoopMode.Timed ? Mathf.Max(0, _litUntil - Time.time) : 0;

        bool _lit;
        float _litUntil;
        Light _light;
        List<Material> _rimMats = new List<Material>();
        Material _orbMat;
        TextMesh _label;
        string _baseLabel;
        Vector3 _prevRim;

        public override bool IsOn => Mode != HoopMode.Counter && Lit;
        public override Color SignalColor => HoopColor;
        public bool AimTarget => AimAssist && !DunkOnly && !RequiresRebound && isActiveAndEnabled;

        public static Hoop Create(string name, Transform parent, Vector3 localPos, float yaw, Color color,
            HoopMode mode, string label = null)
        {
            var root = Build.Group(name, parent, localPos, yaw);
            var hoop = root.AddComponent<Hoop>();
            hoop.HoopColor = color;
            hoop.Mode = mode;
            hoop.DisplayName = name;
            hoop.BuildVisuals(label);
            return hoop;
        }

        void Awake() => All.Add(this);
        void OnDestroy() => All.Remove(this);

        void BuildVisuals(string label)
        {
            var t = transform;
            var model = ModelLibrary.Spawn("Hoop", t, Vector3.zero);
            if (model != null)
            {
                // pole + base sit BEHIND the rim (local -Z); the rim faces local +Z
                ModelLibrary.FaceByBounds(model, -1f);
                ModelLibrary.Tint(model, "HoopPad", HoopColor * 0.85f);
                _rimMats = ModelLibrary.Materials(model, "Rim");
            }
            else
            {
                var pole = Mat.Color("#3B4252", 0.5f, 0.5f);
                var pad = Mat.Color(HoopColor * 0.85f);
                Build.Box("Base", t, new Vector3(0, 0.15f, -1.1f), new Vector3(0.9f, 0.3f, 0.9f), pad, false);
                Build.Prim(PrimitiveType.Cylinder, "Pole", t, new Vector3(0, 1.8f, -1.1f), new Vector3(0.16f, 1.8f, 0.16f), pole, false);
                Build.Box("Arm", t, new Vector3(0, 3.35f, -0.8f), new Vector3(0.12f, 0.12f, 0.62f), pole, false);
                Build.Box("Board", t, new Vector3(0, 3.35f, -0.45f), new Vector3(1.6f, 1.05f, 0.06f), Mat.Color("#F7F9FC", 0.8f), false);
                Build.Box("Square", t, new Vector3(0, 3.24f, -0.415f), new Vector3(0.6f, 0.4f, 0.01f), Mat.Color("#E8772E"), false);
                var rimMat = Mat.Unique(Mat.Hex("#FF6A1A"));
                _rimMats.Add(rimMat);
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2;
                    Build.Box("RimVis", t, new Vector3(Mathf.Cos(a) * RimRadius, RimHeight, Mathf.Sin(a) * RimRadius),
                        new Vector3(0.04f, 0.04f, 0.14f), rimMat, false, Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
                }
                var net = Build.Prim(PrimitiveType.Cylinder, "Net", t, new Vector3(0, RimHeight - 0.22f, 0),
                    new Vector3(0.5f, 0.2f, 0.5f), Mat.Glass(new Color(1, 1, 1, 0.45f)), false);
                net.name = "Net";
            }

            // ---- colliders (independent from the visuals so physics always matches) ----
            Build.Collider("BoardCol", t, new Vector3(0, 3.35f, -0.45f), new Vector3(1.6f, 1.05f, 0.08f));
            Build.Collider("ArmCol", t, new Vector3(0, 3.35f, -0.8f), new Vector3(0.12f, 0.12f, 0.62f));
            Build.Collider("PoleCol", t, new Vector3(0, 1.8f, -1.1f), new Vector3(0.22f, 3.6f, 0.22f));
            Build.Collider("BaseCol", t, new Vector3(0, 0.15f, -1.1f), new Vector3(0.9f, 0.3f, 0.9f));
            int segs = 14;
            for (int i = 0; i < segs; i++)
            {
                float a = (i + 0.5f) / segs * Mathf.PI * 2;
                var seg = Build.Collider("RimSeg" + i, t,
                    new Vector3(Mathf.Cos(a) * RimRadius, RimHeight, Mathf.Sin(a) * RimRadius),
                    new Vector3(0.035f, 0.035f, 2 * Mathf.PI * RimRadius / segs),
                    Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
                seg.GetComponent<BoxCollider>().sharedMaterial = Basketball.BallPhysics;
            }

            var rim = new GameObject("RimCenter");
            rim.transform.SetParent(t, false);
            rim.transform.localPosition = new Vector3(0, RimHeight, 0);
            Rim = rim.transform;
            _prevRim = Rim.position;

            // status orb above the backboard
            _orbMat = Mat.Glow(HoopColor * 0.4f, HoopColor * 0.2f, 1f);
            Build.Prim(PrimitiveType.Sphere, "Orb", t, new Vector3(0, 4.15f, -0.45f), Vector3.one * 0.3f, _orbMat, false);
            _light = Build.PointLight(t, new Vector3(0, RimHeight + 0.3f, 0.3f), HoopColor, 6f, 0f);

            _baseLabel = label;
            if (!string.IsNullOrEmpty(label))
                _label = Build.Label(label, t, new Vector3(0, 4.7f, -0.45f), 0.55f, Color.white, 0, true);
            SetLitVisual(false);
        }

        void FixedUpdate()
        {
            var p = Rim.position;
            Velocity = (p - _prevRim) / Time.fixedDeltaTime;
            _prevRim = p;
        }

        void Update()
        {
            bool lit = Lit;
            if (Mode == HoopMode.Timed && _lit)
            {
                if (!lit)
                {
                    _lit = false;
                    SetLitVisual(false);
                    GameAudio.PlayAt("unclick", RimCenter, 0.6f);
                    if (_label != null) _label.text = _baseLabel;
                }
                else
                {
                    // flicker as time runs out
                    float left = TimeLeft;
                    bool on = left > 3f || Mathf.Repeat(Time.time * 6f, 1f) > 0.35f;
                    SetLitVisual(on);
                    if (_label != null) _label.text = (_baseLabel ?? "") + "\n" + Mathf.CeilToInt(left);
                    if (left < 3f && Mathf.Repeat(Time.time, 1f) < Time.deltaTime) GameAudio.Play("tick", 0.5f);
                }
            }
        }

        public void RegisterScore(Basketball ball)
        {
            if (DunkOnly && !ball.IsDunk)
            {
                Hud.Toast("DUNK ONLY! Get close with the ball and press {DUNK}!", HoopColor);
                GameAudio.PlayAt("buzz", RimCenter, 0.4f);
                return;
            }
            if (RequiresRebound && !ball.ViaRebound)
            {
                Hud.Toast("This hoop needs a bank shot off a Rebound Board!", HoopColor);
                GameAudio.PlayAt("buzz", RimCenter, 0.4f);
                return;
            }
            if (RequiredOwner != null && ball != RequiredOwner.MyBall)
            {
                Hud.Toast("Wrong ball! Only " + RequiredOwner.name.ToUpperInvariant() + "'s ball counts here.", HoopColor, 1.6f);
                GameAudio.PlayAt("buzz", RimCenter, 0.4f);
                return;
            }
            if (MinShotDistance > 0f && !ball.IsDunk && ball.LastThrower == BallOwner.Player)
            {
                var o = ball.ShotOrigin - RimCenter;
                if (new Vector2(o.x, o.z).magnitude < MinShotDistance - 0.25f)
                {
                    Hud.Toast("Too close! Shoot from behind the glowing line.", HoopColor);
                    GameAudio.PlayAt("buzz", RimCenter, 0.4f);
                    ball.ScheduleDespawn(1.5f);
                    return;
                }
            }
            if (!AcceptPlayerShots && ball.LastThrower == BallOwner.Player)
            {
                Hud.Toast("Wrong hoop! Shoot at YOUR hoop!", Color.white);
                return;
            }

            GameAudio.PlayAt(ball.IsDunk ? "dunk" : "swish", RimCenter, 1f);
            if (ball.LastThrower == BallOwner.Player)
                Rumble.Pulse(ball.IsDunk ? 0.8f : 0.15f, ball.IsDunk ? 1f : 0.45f, ball.IsDunk ? 0.3f : 0.15f);
            Effects.Swish(RimCenter + Vector3.down * 0.3f, HoopColor);

            if (Mode == HoopMode.Latch && LightByHang)
            {
                if (!_lit) Hud.Toast("Now keep holding {DUNK} - HANG for " + HangSeconds.ToString("0") + " seconds!", HoopColor, 1.6f);
            }
            else if (Mode == HoopMode.Latch)
            {
                if (!_lit) GameAudio.Play("chime", 0.8f);
                _lit = true;
                SetLitVisual(true);
            }
            else if (Mode == HoopMode.Timed)
            {
                _lit = true;
                _litUntil = Time.time + LitDuration;
                GameAudio.Play("chime", 0.8f);
                SetLitVisual(true);
            }
            else
            {
                StartCoroutine(Flash());
            }
            ball.ScheduleDespawn(2.0f);
            Scored?.Invoke(this, ball);
        }

        System.Collections.IEnumerator Flash()
        {
            SetLitVisual(true);
            yield return new WaitForSeconds(0.8f);
            SetLitVisual(false);
        }

        /// <summary>Called every frame while Zander hangs on this rim.</summary>
        public void AddHangTime(float dt)
        {
            if (!LightByHang || _lit) return;
            _hang += dt;
            if (_label != null) _label.text = (_baseLabel ?? "") + "\nHANG " + Mathf.Max(0f, HangSeconds - _hang).ToString("0.0");
            if (_hang >= HangSeconds)
            {
                _lit = true;
                SetLitVisual(true);
                GameAudio.Play("chime", 0.9f);
                Hud.Toast("HANG TIME! Hoop lit!", HoopColor, 1.2f);
                if (_label != null) _label.text = _baseLabel;
            }
        }

        /// <summary>Zander let go of the rim.</summary>
        public void EndHang()
        {
            if (!LightByHang || _lit) return;
            if (_hang > 0.2f)
                Hud.Toast("Let go too soon! Hold {DUNK} for " + HangSeconds.ToString("0") + " seconds.", Color.white, 1.4f);
            _hang = 0f;
            if (_label != null) _label.text = _baseLabel;
        }

        public void ResetLight()
        {
            _lit = false;
            _litUntil = 0;
            SetLitVisual(false);
            if (_label != null) _label.text = _baseLabel;
        }

        public void ForceLit()
        {
            _lit = true;
            _litUntil = Time.time + LitDuration;
            SetLitVisual(true);
        }

        void SetLitVisual(bool on)
        {
            foreach (var m in _rimMats)
            {
                if (m == null) continue;
                m.color = on ? Color.Lerp(HoopColor, Color.white, 0.3f) : Mat.Hex("#FF6A1A");
                Mat.SetEmission(m, on ? HoopColor * 2.2f : Color.black);
            }
            if (_orbMat != null)
            {
                _orbMat.color = on ? HoopColor : HoopColor * 0.35f;
                Mat.SetEmission(_orbMat, on ? HoopColor * 2.5f : HoopColor * 0.15f);
            }
            if (_light != null) _light.intensity = on ? 2.5f : 0f;
        }
    }
}
