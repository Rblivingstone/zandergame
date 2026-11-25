using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    public enum PlateKind
    {
        Feet,  // BLUE plates: only Zander standing on them
        Ball   // ORANGE plates: only a basketball resting on them
    }

    /// <summary>A pressure plate. Origin on the floor; top surface ~0.18 m up.</summary>
    public class PressurePlate : Signal
    {
        public static readonly List<PressurePlate> All = new List<PressurePlate>();
        public PlateKind Kind;
        public bool Latch;          // stays pressed forever once pressed
        public const float TopY = 0.18f;
        public Vector3 BallRestPoint => transform.position + Vector3.up * (TopY + Basketball.Radius + 0.02f);

        Transform _top;
        Vector3 _topRest;
        bool _pressed, _latched;
        Material _glow;
        Light _light;
        static readonly Collider[] Buf = new Collider[24];

        public override bool IsOn => _latched || _pressed;
        public override Color SignalColor => KindColor(Kind);
        public bool Pressed => IsOn;

        public static Color KindColor(PlateKind k) => k == PlateKind.Feet ? Mat.Hex("#4AA3FF") : Mat.Hex("#FF8A1F");

        public static PressurePlate Create(string name, Transform parent, Vector3 localPos, PlateKind kind, bool latch = false)
        {
            var root = Build.Group(name, parent, localPos);
            var p = root.AddComponent<PressurePlate>();
            p.Kind = kind;
            p.Latch = latch;
            p.BuildVisuals();
            return p;
        }

        void Awake() => All.Add(this);
        void OnDestroy() => All.Remove(this);

        void BuildVisuals()
        {
            var t = transform;
            Color kc = KindColor(Kind);
            var model = ModelLibrary.Spawn("PressurePlate", t, Vector3.zero);
            _glow = Mat.Glow(kc, kc * 0.3f, 1f);
            if (model != null)
            {
                _top = ModelLibrary.FindDeep(model.transform, "PlateTop");
                if (_top != null)
                    foreach (var r in _top.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _glow;
            }
            else
            {
                Build.Box("Base", t, new Vector3(0, 0.05f, 0), new Vector3(1.6f, 0.1f, 1.6f), Mat.Color("#565D6B", 0.1f), false);
                _top = Build.Box("PlateTop", t, new Vector3(0, 0.14f, 0), new Vector3(1.3f, 0.08f, 1.3f), _glow, false).transform;
            }
            if (_top != null) _topRest = _top.localPosition;

            // icon painted on top: a ball (circle) or two footprints
            var iconParent = _top != null ? _top : t;
            var iconMat = Mat.Color(Kind == PlateKind.Ball ? "#FFFFFF" : "#1A3A6B");
            if (Kind == PlateKind.Ball)
            {
                var icon = Build.Prim(PrimitiveType.Cylinder, "BallIcon", t, new Vector3(0, TopY + 0.005f, 0),
                    new Vector3(0.6f, 0.005f, 0.6f), Mat.Color("#E8772E"), false);
                icon.transform.SetParent(iconParent, true);
                Build.Box("SeamA", icon.transform, Vector3.zero, new Vector3(1f, 1.2f, 0.06f), iconMat, false).transform.SetParent(icon.transform, false);
                Build.Box("SeamB", icon.transform, Vector3.zero, new Vector3(0.06f, 1.2f, 1f), iconMat, false).transform.SetParent(icon.transform, false);
            }
            else
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    var f = Build.Prim(PrimitiveType.Cylinder, "Foot", t, new Vector3(s * 0.18f, TopY + 0.005f, 0),
                        new Vector3(0.22f, 0.005f, 0.42f), iconMat, false);
                    f.transform.SetParent(iconParent, true);
                }
            }

            // physical lip so a ball resting on a BALL plate doesn't roll off
            Build.Collider("PlateCol", t, new Vector3(0, 0.09f, 0), new Vector3(1.6f, 0.18f, 1.6f));
            if (Kind == PlateKind.Ball)
            {
                var lipMat = Mat.Color("#3B4252");
                float h = 0.1f, e = 0.62f;
                Build.Box("Lip", t, new Vector3(0, TopY + h / 2, e), new Vector3(1.3f, h, 0.06f), lipMat);
                Build.Box("Lip", t, new Vector3(0, TopY + h / 2, -e), new Vector3(1.3f, h, 0.06f), lipMat);
                Build.Box("Lip", t, new Vector3(e, TopY + h / 2, 0), new Vector3(0.06f, h, 1.3f), lipMat);
                Build.Box("Lip", t, new Vector3(-e, TopY + h / 2, 0), new Vector3(0.06f, h, 1.3f), lipMat);
            }
            _light = Build.PointLight(t, new Vector3(0, 0.8f, 0), kc, 4f, 0f);
            Build.Label(Kind == PlateKind.Ball ? "BALL" : "STEP", t, new Vector3(0, 0.45f, -0.95f), 0.3f, kc, 0, true);
        }

        void FixedUpdate()
        {
            bool now = false;
            var center = transform.position + Vector3.up * (TopY + 0.35f);
            int n = Physics.OverlapBoxNonAlloc(center, new Vector3(0.62f, 0.35f, 0.62f), Buf, transform.rotation, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n && !now; i++)
            {
                var c = Buf[i];
                if (Kind == PlateKind.Feet && c is CharacterController) now = true;
                if (Kind == PlateKind.Ball && c.attachedRigidbody != null)
                {
                    var b = c.attachedRigidbody.GetComponent<Basketball>();
                    if (b != null && !b.IsHeld && b.Body.GetVelocity().magnitude < 2.5f) now = true;
                }
            }
            if (now != _pressed)
            {
                _pressed = now;
                if (now && Latch) _latched = true;
                GameAudio.PlayAt(now ? "click" : "unclick", transform.position, 0.8f);
            }
        }

        void Update()
        {
            bool on = IsOn;
            if (_top != null)
                _top.localPosition = Vector3.Lerp(_top.localPosition, _topRest + (on ? Vector3.down * 0.06f : Vector3.zero),
                    Time.deltaTime * 12f);
            Color kc = KindColor(Kind);
            Mat.SetEmission(_glow, on ? kc * 2.2f : kc * 0.25f);
            if (_light != null) _light.intensity = on ? 2f : 0f;
        }
    }
}
