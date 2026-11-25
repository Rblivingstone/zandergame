using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Small helpers that paper over API renames between Unity 2022 LTS and Unity 6
    /// (Rigidbody.velocity -> linearVelocity, PhysicMaterial -> PhysicsMaterial, ...).
    /// </summary>
    public static class Compat
    {
        public static Vector3 GetVelocity(this Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(this Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(this Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

#if UNITY_6000_0_OR_NEWER
        public static PhysicsMaterial MakePhysicsMaterial(string name, float bounce, float friction, bool maxBounce)
        {
            return new PhysicsMaterial(name)
            {
                bounciness = bounce,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = maxBounce ? PhysicsMaterialCombine.Maximum : PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average
            };
        }
#else
        public static PhysicMaterial MakePhysicsMaterial(string name, float bounce, float friction, bool maxBounce)
        {
            return new PhysicMaterial(name)
            {
                bounciness = bounce,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = maxBounce ? PhysicMaterialCombine.Maximum : PhysicMaterialCombine.Average,
                frictionCombine = PhysicMaterialCombine.Average
            };
        }
#endif

        public static Font DefaultFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return f;
        }
    }
}
