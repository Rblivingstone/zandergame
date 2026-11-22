using UnityEngine;

namespace ZanderGame
{
    public static class Ballistics
    {
        public static float G => Mathf.Abs(Physics.gravity.y);

        /// <summary>
        /// Launch velocity that goes from 'from' to 'to' and peaks 'arc' metres above
        /// the higher of the two points. Also returns the flight time.
        /// </summary>
        public static Vector3 Solve(Vector3 from, Vector3 to, float arc, out float time)
        {
            float g = G;
            float apex = Mathf.Max(from.y, to.y) + Mathf.Max(0.1f, arc);
            float up = apex - from.y;
            float down = apex - to.y;
            float vy = Mathf.Sqrt(2f * g * up);
            float tUp = vy / g;
            float tDown = Mathf.Sqrt(2f * down / g);
            time = tUp + tDown;
            var flat = new Vector3(to.x - from.x, 0, to.z - from.z);
            var vxz = flat / time;
            return new Vector3(vxz.x, vy, vxz.z);
        }

        /// <summary>A nice basketball arc: higher for longer shots.</summary>
        public static float ShotArc(Vector3 from, Vector3 to)
        {
            float d = new Vector2(to.x - from.x, to.z - from.z).magnitude;
            return Mathf.Clamp(1.1f + d * 0.22f, 1.2f, 4.5f);
        }

        /// <summary>Solve against a moving target by iterating on flight time.</summary>
        public static Vector3 SolveLeading(Vector3 from, Vector3 target, Vector3 targetVel, float arc, out float time)
        {
            var v = Solve(from, target, arc, out time);
            for (int i = 0; i < 3; i++)
                v = Solve(from, target + targetVel * time, arc, out time);
            return v;
        }
    }

    /// <summary>
    /// Anything that can be "on" or "off" and drive puzzle elements:
    /// hoops, pressure plates, sequences...
    /// </summary>
    public abstract class Signal : MonoBehaviour
    {
        public abstract bool IsOn { get; }
        public virtual Color SignalColor => new Color(1f, 0.6f, 0.15f);
    }
}
