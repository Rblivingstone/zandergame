using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ZanderGame
{
    /// <summary>
    /// Plays the animation clips baked into a Blender FBX (Idle, Run, Shoot, ...)
    /// using the Playables API, so no Animator Controller asset is needed.
    /// Looping "base" states cross-fade; one-shots play over them and return.
    /// </summary>
    public class ClipAnimator
    {
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly List<AnimationClipPlayable> _playables = new List<AnimationClipPlayable>();
        readonly List<AnimationClip> _clips = new List<AnimationClip>();
        readonly Dictionary<string, int> _index = new Dictionary<string, int>();
        float[] _weights;
        bool[] _loop;
        int _base = -1;
        int _oneShot = -1;
        float _fade = 0.15f;
        float _baseSpeed = 1f;

        public static readonly HashSet<string> LoopingNames = new HashSet<string>
        {
            "idle", "run", "dribble", "dribblerun", "celebrate", "walk", "block", "rimhang"
        };

        public static ClipAnimator TryCreate(GameObject modelRoot, string resourcePath)
        {
            if (modelRoot == null) return null;
            var all = Resources.LoadAll<AnimationClip>(resourcePath);
            if (all == null || all.Length == 0) return null;
            var animator = modelRoot.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                // the FBX root is the first child of our container
                var host = modelRoot.transform.childCount > 0 ? modelRoot.transform.GetChild(0).gameObject : modelRoot;
                animator = host.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var ca = new ClipAnimator();
            foreach (var c in all)
            {
                if (c == null || c.name.StartsWith("__preview__")) continue;
                string key = Normalize(c.name);
                if (ca._index.ContainsKey(key)) continue;
                ca._index[key] = ca._clips.Count;
                ca._clips.Add(c);
            }
            if (ca._clips.Count == 0) return null;

            ca._graph = PlayableGraph.Create(modelRoot.name + "_Anim");
            ca._graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(ca._graph, "Animation", animator);
            ca._mixer = AnimationMixerPlayable.Create(ca._graph, ca._clips.Count);
            output.SetSourcePlayable(ca._mixer);
            ca._weights = new float[ca._clips.Count];
            ca._loop = new bool[ca._clips.Count];
            for (int i = 0; i < ca._clips.Count; i++)
            {
                var p = AnimationClipPlayable.Create(ca._graph, ca._clips[i]);
                p.SetApplyFootIK(false);
                ca._graph.Connect(p, 0, ca._mixer, i);
                ca._mixer.SetInputWeight(i, 0f);
                ca._playables.Add(p);
                ca._loop[i] = LoopingNames.Contains(Normalize(ca._clips[i].name));
            }
            ca._graph.Play();
            return ca;
        }

        static string Normalize(string clipName)
        {
            string n = clipName;
            int bar = n.LastIndexOf('|');
            if (bar >= 0) n = n.Substring(bar + 1);
            return n.Trim().ToLowerInvariant();
        }

        public bool Has(string name) => _index.ContainsKey(name.ToLowerInvariant());
        public bool PlayingOneShot => _oneShot >= 0;

        public string CurrentBase => _base >= 0 ? Normalize(_clips[_base].name) : "";

        public void SetBase(string name, float speed = 1f, float fade = 0.15f)
        {
            _baseSpeed = speed;
            if (!_index.TryGetValue(name.ToLowerInvariant(), out int i)) return;
            if (_base >= 0) _playables[_base].SetSpeed(_baseSpeed);
            if (i == _base) return;
            _base = i;
            _fade = fade;
            if (!_loop[i]) _playables[i].SetTime(0);
            _playables[i].SetSpeed(_baseSpeed);
        }

        public void PlayOnce(string name, float speed = 1f, float fade = 0.08f)
        {
            if (!_index.TryGetValue(name.ToLowerInvariant(), out int i)) return;
            _oneShot = i;
            _fade = fade;
            _playables[i].SetTime(0);
            _playables[i].SetSpeed(speed);
        }

        /// <summary>Hold the one-shot's final pose (e.g. boss Defeat).</summary>
        public void PlayAndHold(string name)
        {
            if (!_index.TryGetValue(name.ToLowerInvariant(), out int i)) return;
            _oneShot = -1;
            _base = i;
            _loop[i] = false;
            _playables[i].SetTime(0);
            _playables[i].SetSpeed(1);
        }

        public void Tick(float dt)
        {
            if (!_graph.IsValid()) return;
            // one-shot finished?
            if (_oneShot >= 0 && _playables[_oneShot].GetTime() >= _clips[_oneShot].length) _oneShot = -1;

            int target = _oneShot >= 0 ? _oneShot : _base;
            float k = _fade <= 0 ? 1f : Mathf.Clamp01(dt / _fade);
            float total = 0f;
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = Mathf.MoveTowards(_weights[i], i == target ? 1f : 0f, k);
                total += _weights[i];
            }
            for (int i = 0; i < _weights.Length; i++)
            {
                float w = total > 0 ? _weights[i] / total : 0f;
                _mixer.SetInputWeight(i, w);
                var p = _playables[i];
                double len = _clips[i].length;
                if (len <= 0) continue;
                double t = p.GetTime();
                if (_loop[i] && t >= len) p.SetTime(t % len);
                else if (!_loop[i] && t > len && i != _oneShot) p.SetTime(len);
            }
        }

        public void Destroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
