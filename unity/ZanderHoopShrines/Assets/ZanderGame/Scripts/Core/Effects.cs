using UnityEngine;

namespace ZanderGame
{
    /// <summary>Particle bursts (sparkles, confetti) built at runtime.</summary>
    public static class Effects
    {
        static Material _particleMat;

        static Material ParticleMat
        {
            get
            {
                if (_particleMat == null)
                {
                    var sh = Shader.Find("Particles/Standard Unlit");
                    if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
                    if (sh == null) sh = Mat.Unlit;
                    _particleMat = new Material(sh) { name = "ZG_Particles" };
                }
                return _particleMat;
            }
        }

        public static ParticleSystem Burst(Vector3 pos, Color color, int count, float speed, float size = 0.12f,
            float life = 0.8f, bool gravity = true)
        {
            var go = new GameObject("FX_Burst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));
            main.gravityModifier = gravity ? 0.6f : 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat;
            ps.Play();
            return ps;
        }

        public static void Confetti(Vector3 pos, int count = 120)
        {
            Color[] cols = { Mat.Hex("#FF4D4D"), Mat.Hex("#FFC21A"), Mat.Hex("#3CE07A"), Mat.Hex("#4AA3FF"), Mat.Hex("#C86BFF") };
            foreach (var c in cols) Burst(pos, c, count / cols.Length, 9f, 0.18f, 2.5f);
        }

        public static void Swish(Vector3 pos, Color color)
        {
            Burst(pos, color, 24, 3f, 0.14f, 0.9f);
            Burst(pos + Vector3.up * 0.2f, Color.white, 10, 2f, 0.1f, 0.6f);
        }
    }
}
