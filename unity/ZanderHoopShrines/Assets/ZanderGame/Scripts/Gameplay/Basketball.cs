using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    public enum BallOwner { None, Player, Boss }

    /// <summary>
    /// A physical basketball. Detects baskets itself (by crossing a rim plane
    /// downward inside the rim) and tells the Hoop.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Basketball : MonoBehaviour
    {
        public static readonly List<Basketball> All = new List<Basketball>();
        public const float Radius = 0.14f;

        public Rigidbody Body { get; private set; }
        public SphereCollider Col { get; private set; }
        public bool IsHeld { get; private set; }
        public bool IsDocked { get; private set; }
        public object Holder { get; private set; }
        public BallDispenser Source;
        public BallOwner LastThrower = BallOwner.None;
        public bool PlayerCanGrab = true;
        public bool IsDunk;
        public bool ViaRebound;
        public Vector3 ShotOrigin;
        public float ReleaseTime;

        Vector3 _lastPos;
        Hoop _lastScoredHoop;
        float _lastScoreTime;
        float _despawnAt = -1f;
        bool _touchedSinceRelease;
        float _lastBounceSound;

        static Material _fallbackMat;

        public static Basketball Create(Vector3 position, Transform parent = null)
        {
            var go = new GameObject("Basketball");
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.layer = Build.IgnoreRaycastLayer;

            var model = ModelLibrary.Spawn("Basketball", go.transform, Vector3.zero);
            if (model != null)
            {
                // Model is authored at radius 0.14 around its origin - normalise just in case.
                var b = ModelLibrary.WorldBounds(model);
                if (b.size.y > 0.01f) model.transform.localScale *= (Radius * 2f) / b.size.y;
                Build.SetLayerRecursive(model, Build.IgnoreRaycastLayer);
            }
            else
            {
                if (_fallbackMat == null) _fallbackMat = Mat.Color("#E8772E", 0.2f);
                var s = Build.Prim(PrimitiveType.Sphere, "BallMesh", go.transform, Vector3.zero, Vector3.one * Radius * 2,
                    _fallbackMat, false);
                var seam = Mat.Color("#1E140E");
                Build.Prim(PrimitiveType.Cylinder, "Seam", s.transform, Vector3.zero, new Vector3(1.02f, 0.01f, 1.02f), seam, false);
                Build.Prim(PrimitiveType.Cylinder, "Seam2", s.transform, Vector3.zero, new Vector3(1.02f, 0.01f, 1.02f), seam,
                    false, Quaternion.Euler(90, 0, 0));
                Build.SetLayerRecursive(s, Build.IgnoreRaycastLayer);
            }

            var col = go.AddComponent<SphereCollider>();
            col.radius = Radius;
            col.sharedMaterial = BallPhysics;
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            rb.SetDamping(0f, 0.6f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return go.AddComponent<Basketball>();
        }

#if UNITY_6000_0_OR_NEWER
        static PhysicsMaterial _ballPhys;
        public static PhysicsMaterial BallPhysics
#else
        static PhysicMaterial _ballPhys;
        public static PhysicMaterial BallPhysics
#endif
        {
            get
            {
                if (_ballPhys == null) _ballPhys = Compat.MakePhysicsMaterial("Basketball", 0.72f, 0.55f, false);
                return _ballPhys;
            }
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Col = GetComponent<SphereCollider>();
            All.Add(this);
            _lastPos = transform.position;
        }

        void OnDestroy() => All.Remove(this);

        public void Dock(Vector3 position)
        {
            IsDocked = true;
            IsHeld = false;
            Holder = null;
            Body.isKinematic = true;
            Col.enabled = true;
            transform.position = position;
            _lastPos = position;
        }

        public void Hold(object holder)
        {
            IsHeld = true;
            IsDocked = false;
            Holder = holder;
            Body.isKinematic = true;
            Col.enabled = false;
            _despawnAt = -1f;
            IsDunk = false;
            ViaRebound = false;
        }

        /// <summary>Let go of the ball with a velocity (shoot / pass / drop).</summary>
        public void Release(Vector3 position, Vector3 velocity, BallOwner thrower)
        {
            IsHeld = false;
            IsDocked = false;
            Holder = null;
            transform.position = position;
            _lastPos = position;
            Body.isKinematic = false;
            Col.enabled = true;
            Body.SetDamping(0f, 0.6f);
            Body.SetVelocity(velocity);
            Body.angularVelocity = transform.right * -8f;
            LastThrower = thrower;
            ShotOrigin = position;
            ReleaseTime = Time.time;
            _touchedSinceRelease = false;
            ViaRebound = false;
        }

        /// <summary>Redirect an in-flight ball (Rebound Boards).</summary>
        public void Redirect(Vector3 position, Vector3 velocity)
        {
            transform.position = position;
            _lastPos = position;
            Body.position = position;
            Body.SetVelocity(velocity);
            Body.SetDamping(0f, 0.6f);
            ViaRebound = true;
            _touchedSinceRelease = false;
        }

        public void ScheduleDespawn(float seconds)
        {
            if (Source == null && !(Holder is BossController) && LastThrower != BallOwner.Boss) return;
            _despawnAt = Time.time + seconds;
        }

        void FixedUpdate()
        {
            if (IsHeld || IsDocked)
            {
                _lastPos = transform.position;
                return;
            }
            Vector3 cur = Body.position;
            // Basket detection: crossing the rim plane downward inside the ring.
            foreach (var hoop in Hoop.All)
            {
                if (hoop == null || !hoop.isActiveAndEnabled) continue;
                Vector3 c = hoop.RimCenter;
                if (_lastPos.y > c.y && cur.y <= c.y)
                {
                    float t = (_lastPos.y - c.y) / Mathf.Max(0.0001f, _lastPos.y - cur.y);
                    Vector3 p = Vector3.Lerp(_lastPos, cur, t);
                    float d = new Vector2(p.x - c.x, p.z - c.z).magnitude;
                    if (d < hoop.RimRadius - Radius * 0.3f)
                    {
                        if (_lastScoredHoop == hoop && Time.time - _lastScoreTime < 1.5f) continue;
                        _lastScoredHoop = hoop;
                        _lastScoreTime = Time.time;
                        hoop.RegisterScore(this);
                    }
                }
            }
            _lastPos = cur;

            // Rebound Boards catch any ball arriving at their front (no exact collision needed)
            foreach (var board in ReboundBoard.AllBoards)
                if (board != null && board.TryCatch(this, cur)) break;

            if (cur.y < -6f) LostBall();
            if (_despawnAt > 0 && Time.time > _despawnAt) Poof();
        }

        void OnCollisionEnter(Collision c)
        {
            if (!_touchedSinceRelease)
            {
                _touchedSinceRelease = true;
                Body.SetDamping(0.15f, 0.8f);
            }
            float v = c.relativeVelocity.magnitude;
            if (v > 1.2f && Time.time - _lastBounceSound > 0.08f)
            {
                _lastBounceSound = Time.time;
                bool rim = c.collider != null && c.collider.name.StartsWith("RimSeg");
                GameAudio.PlayAt(rim ? "rim" : "bounce", transform.position, Mathf.Clamp01(v / 8f) + 0.2f,
                    Random.Range(0.9f, 1.1f));
            }
            var board = c.collider != null ? c.collider.GetComponentInParent<ReboundBoard>() : null;
            if (board != null) board.OnBallHit(this);
        }

        public void LostBall()
        {
            if (Source != null || LastThrower == BallOwner.Boss) Poof();
            else
            {
                // Unowned ball: put it back somewhere sensible.
                Body.SetVelocity(Vector3.zero);
                transform.position = GameManager.Instance != null ? GameManager.Instance.CheckpointPosition + Vector3.up * 2f
                    : Vector3.up * 2f;
                _lastPos = transform.position;
            }
        }

        public void Poof()
        {
            Effects.Burst(transform.position, new Color(1f, 0.6f, 0.2f), 14, 2f);
            Destroy(gameObject);
        }
    }
}
