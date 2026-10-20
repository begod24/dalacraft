using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Dala
{
    /// <summary>
    /// Plays the animation takes baked into a Blender FBX through a Playables mixer with cross-fades —
    /// no Animator Controller asset needed. Clips are matched by name suffix ("Walk" finds "Cow_Walk").
    /// </summary>
    public sealed class ClipPlayer
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly List<AnimationClipPlayable> plays = new();
        readonly List<AnimationClip> clips = new();
        readonly Dictionary<string, int> index = new();
        float[] weights;
        int target = -1, oneShot = -1;
        float fade = 0.25f, oneShotEnd;

        public ClipPlayer(Animator animator, IEnumerable<AnimationClip> source, string name)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            graph = PlayableGraph.Create(name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            foreach (var c in source)
                if (c != null && !c.name.StartsWith("__preview__")) clips.Add(c);
            mixer = AnimationMixerPlayable.Create(graph, clips.Count);
            weights = new float[clips.Count];
            for (int i = 0; i < clips.Count; i++)
            {
                var p = AnimationClipPlayable.Create(graph, clips[i]);
                graph.Connect(p, 0, mixer, i);
                mixer.SetInputWeight(i, 0);
                plays.Add(p);
                string n = clips[i].name;
                int us = n.LastIndexOf('_');
                index[n] = i;
                if (us >= 0) index[n.Substring(us + 1)] = i;
            }
            var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        public bool Has(string clip) => index.ContainsKey(clip);

        /// <summary>Cross-fades to a looping clip (no-op if it is already the target).</summary>
        public void Play(string clip, float fadeTime = 0.25f, float speed = 1f)
        {
            if (!index.TryGetValue(clip, out int i)) return;
            plays[i].SetSpeed(speed);
            if (i == target) return;
            if (weights[i] < 0.01f) plays[i].SetTime(0);
            target = i;
            fade = Mathf.Max(0.01f, fadeTime);
        }

        /// <summary>Plays a clip once over the current one (e.g. a pick-axe swing), then returns.</summary>
        public void PlayOnce(string clip, float speed = 1f)
        {
            if (!index.TryGetValue(clip, out int i)) return;
            plays[i].SetTime(0);
            plays[i].SetSpeed(speed);
            oneShot = i;
            oneShotEnd = Time.time + clips[i].length / Mathf.Max(0.01f, speed);
            fade = 0.08f;
        }

        public void Tick(float dt)
        {
            if (!graph.IsValid()) return;
            if (oneShot >= 0 && Time.time >= oneShotEnd) { oneShot = -1; fade = 0.15f; }
            int cur = oneShot >= 0 ? oneShot : target;
            float step = dt / fade;
            float sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == cur ? 1f : 0f, step);
                sum += weights[i];
            }
            for (int i = 0; i < weights.Length; i++)
                mixer.SetInputWeight(i, sum > 0 ? weights[i] / sum : 0);
        }

        public void Destroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>Instantiates a Blender FBX from Resources and wires a ClipPlayer to its animations.</summary>
        public static GameObject Spawn(string resourcePath, Transform parent, out ClipPlayer player)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            player = null;
            if (prefab == null) { Debug.LogWarning($"Missing model Resources/{resourcePath}"); return null; }
            var go = Object.Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            player = new ClipPlayer(anim, Resources.LoadAll<AnimationClip>(resourcePath), resourcePath);
            return go;
        }
    }
}
