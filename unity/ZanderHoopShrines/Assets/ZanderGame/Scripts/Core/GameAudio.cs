using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// All sound effects are synthesised at start-up, so the game ships without
    /// audio files. Call GameAudio.Play("swish").
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        static GameAudio _inst;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        AudioSource[] _sources;
        int _next;
        AudioSource _music;
        const int Rate = 22050;

        public static void Init(GameObject host)
        {
            if (_inst != null) return;
            _inst = host.AddComponent<GameAudio>();
            _inst.Build();
        }

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (_inst == null || !_inst._clips.TryGetValue(name, out var clip)) return;
            var src = _inst._sources[_inst._next];
            _inst._next = (_inst._next + 1) % _inst._sources.Length;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        public static void PlayAt(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            // Distance attenuation (2D sources, so it works in any scene setup).
            var cam = Camera.main;
            float v = volume;
            if (cam != null)
            {
                float d = Vector3.Distance(cam.transform.position, pos);
                v *= Mathf.Clamp01(1.2f - d / 40f);
            }
            if (v > 0.02f) Play(name, v, pitch);
        }

        void Build()
        {
            _sources = new AudioSource[10];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].spatialBlend = 0f;
            }

            Add("bounce", 0.12f, (t, i) => Sine(t, 90 - 200 * t) * Env(t, 0.003f, 0.11f) * 0.9f);
            Add("swish", 0.45f, (t, i) => Noise(i) * Env(t, 0.03f, 0.4f) * 0.35f * (0.6f + 0.4f * Mathf.Sin(t * 60)));
            Add("rim", 0.35f, (t, i) => (Sine(t, 620) * 0.5f + Sine(t, 1240) * 0.3f) * Env(t, 0.002f, 0.33f) * 0.5f);
            Add("click", 0.12f, (t, i) => (Square(t, 180) * 0.4f + Noise(i) * 0.2f) * Env(t, 0.002f, 0.1f));
            Add("unclick", 0.12f, (t, i) => Square(t, 120) * 0.35f * Env(t, 0.002f, 0.1f));
            Add("gate", 1.6f, (t, i) => (Saw(t, 45) * 0.4f + Noise(i) * 0.25f) * Env(t, 0.1f, 1.5f) * 0.6f);
            Add("chime", 1.0f, (t, i) => Arp(t, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f) * 0.4f);
            Add("solve", 1.8f, (t, i) => Arp(t, new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.13f) * 0.45f);
            Add("buzz", 0.6f, (t, i) => Square(t, 110) * Env(t, 0.01f, 0.55f) * 0.35f);
            Add("throw", 0.2f, (t, i) => Noise(i) * Env(t, 0.01f, 0.18f) * 0.25f);
            Add("pickup", 0.15f, (t, i) => Sine(t, 400 + 1600 * t) * Env(t, 0.005f, 0.14f) * 0.35f);
            Add("boing", 0.5f, (t, i) => Sine(t, 180 + 500 * t + 40 * Mathf.Sin(t * 50)) * Env(t, 0.005f, 0.45f) * 0.5f);
            Add("dunk", 0.6f, (t, i) => (Sine(t, 70) * 0.8f + Noise(i) * 0.4f) * Env(t, 0.002f, 0.55f) * 0.8f);
            Add("turn", 0.25f, (t, i) => (Square(t, 300 + 300 * t) * 0.3f) * Env(t, 0.005f, 0.22f));
            Add("stomp", 0.9f, (t, i) => (Sine(t, 45 - 20 * t) * 0.9f + Noise(i) * 0.3f) * Env(t, 0.005f, 0.85f));
            Add("roar", 1.2f, (t, i) => (Saw(t, 90 + 30 * Mathf.Sin(t * 20)) * 0.5f + Noise(i) * 0.2f) * Env(t, 0.1f, 1.1f) * 0.5f);
            Add("cheer", 2.0f, (t, i) => Noise(i) * Env(t, 0.2f, 1.8f) * 0.25f * (0.7f + 0.3f * Mathf.Sin(t * 13)));
            Add("whistle", 0.7f, (t, i) => Sine(t, 2400 + 80 * Mathf.Sin(t * 70)) * Env(t, 0.02f, 0.65f) * 0.3f);
            Add("tick", 0.08f, (t, i) => Sine(t, 1500) * Env(t, 0.001f, 0.07f) * 0.3f);
            Add("splash", 0.5f, (t, i) => Noise(i) * Env(t, 0.01f, 0.45f) * 0.3f);
        }

        delegate float Synth(float t, int i);

        void Add(string name, float seconds, Synth fn)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(fn(i / (float)Rate, i), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            _clips[name] = clip;
        }

        static float Sine(float t, float f) => Mathf.Sin(2 * Mathf.PI * f * t);
        static float Square(float t, float f) => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t));
        static float Saw(float t, float f) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));

        static uint _seed = 1234567;

        static float Noise(int i)
        {
            _seed = _seed * 1664525u + 1013904223u;
            return ((_seed >> 8) / (float)(1 << 24)) * 2f - 1f;
        }

        static float Env(float t, float attack, float length)
        {
            if (t < attack) return t / attack;
            return Mathf.Clamp01(1f - (t - attack) / Mathf.Max(0.0001f, length - attack));
        }

        static float Arp(float t, float[] notes, float step)
        {
            float s = 0;
            for (int k = 0; k < notes.Length; k++)
            {
                float lt = t - k * step;
                if (lt < 0) continue;
                s += Sine(lt, notes[k]) * Env(lt, 0.005f, 0.6f) * 0.5f;
            }
            return s;
        }
    }
}
