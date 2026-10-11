using System.Collections;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Melody shards: one floating shard rests on every checkpoint lantern (so it always lies on the route).
    /// Each shard plays the next note of the level's melody; the exit stays sealed until all are gathered,
    /// and the whole melody plays when the door opens. Levels without checkpoints (the finale) have none.
    /// Separate from the optional secret orbs (<see cref="Secrets"/>).
    /// </summary>
    public static class MelodyShards
    {
        public static int InLevel { get; private set; }
        public static int Collected { get; private set; }
        public static int Remaining => Mathf.Max(0, InLevel - Collected);
        public static bool DoorOpen => Remaining == 0;
        /// <summary>collected, total.</summary>
        public static event System.Action<int, int> Changed;
        public static event System.Action DoorOpened;

        /// <summary>Scale degrees of the level melody (index = shard order). Level number picks a motif.</summary>
        public static int[] Melody { get; private set; } = new int[0];

        static readonly int[][] Motifs =
        {
            new[] { 0, 2, 4, 7, 4, 9 },      // rising triad, the sung fifth
            new[] { 4, 3, 2, 4, 7, 9 },
            new[] { 7, 6, 4, 2, 4, 7 },
            new[] { 0, 4, 2, 5, 4, 7 },
            new[] { 2, 4, 6, 4, 7, 11 },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            LevelBuilder.PostBuild -= OnPostBuild;
            LevelBuilder.PostBuild += OnPostBuild;
        }

        static void OnPostBuild(LevelContext ctx)
        {
            InLevel = 0; Collected = 0;
            var all = ctx.entities.GetComponentsInChildren<CheckpointLantern>(true);
            // Optional header `shardskip: 104 120`: checkpoints (by x) off the main route that get no shard.
            var skip = new System.Collections.Generic.List<float>();
            foreach (var tok in (ctx.data != null ? ctx.data.Get("shardskip", "") : "").Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                if (float.TryParse(tok, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) skip.Add(v);
            var list = new System.Collections.Generic.List<CheckpointLantern>();
            foreach (var l in all) if (!skip.Exists(v => Mathf.Abs(l.transform.position.x - v) < 1f)) list.Add(l);
            var lanterns = list.ToArray();
            System.Array.Sort(lanterns, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            var motif = Motifs[Mathf.Abs(ctx.levelNumber) % Motifs.Length];
            Melody = new int[lanterns.Length];
            for (int i = 0; i < lanterns.Length; i++)
            {
                Melody[i] = motif[i % motif.Length];
                Spawn(lanterns[i].transform.position + new Vector3(0f, 1.25f, 0f), i, ctx.entities);
            }
            InLevel = lanterns.Length;
            if (InLevel > 0) Debug.Log("[LumenWave] Melody shards: " + string.Join(" ", System.Array.ConvertAll(lanterns, l => "(" + l.transform.position.x.ToString("0.#") + "," + l.transform.position.y.ToString("0.#") + ")")));
            Changed?.Invoke(Collected, InLevel);
        }

        static void Spawn(Vector3 pos, int index, Transform parent)
        {
            var go = new GameObject("MelodyShard" + index);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var gem = PropVisuals.MeshObject("Gem", go.transform, PropVisuals.Octahedron(0.2f, 1.8f), PropVisuals.Emissive(ShardColor, 6f));
            PropVisuals.PointLight(go.transform, new Vector3(0, 0, -0.5f), ShardColor, 3.5f, 1.6f);
            go.AddComponent<MelodyShard>().Init(gem.transform, index);
        }

        public static readonly Color ShardColor = new Color(0.55f, 0.95f, 1f);

        public static float NoteFreq(int i) => Harmony.Degree(Melody.Length > 0 ? Melody[Mathf.Clamp(i, 0, Melody.Length - 1)] : 0) * 2f;

        internal static void Collect(int index)
        {
            Collected++;
            Debug.Log("[LumenWave] Melody shard " + index + " collected (" + Collected + "/" + InLevel + ")");
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayNote(NoteFreq(index), 0.5f, 0.9f);
            Changed?.Invoke(Collected, InLevel);
            if (Remaining == 0)
            {
                GameEvents.RaiseMessage("The melody is whole. The door opens.");
                DoorOpened?.Invoke();
                var runner = Object.FindAnyObjectByType<LevelRuntime>();
                if (runner != null) runner.StartCoroutine(PlayMelody(0.55f));
            }
            else GameEvents.RaiseMessage("Melody shard " + Collected + "/" + InLevel);
        }

        static IEnumerator PlayMelody(float delay)
        {
            yield return new WaitForSeconds(delay);
            for (int i = 0; i < Melody.Length; i++)
            {
                if (AudioDirector.Instance != null) AudioDirector.Instance.PlayNote(NoteFreq(i), 0.6f, 0.8f);
                yield return new WaitForSeconds(0.26f);
            }
            if (AudioDirector.Instance != null && Melody.Length > 0) AudioDirector.Instance.PlayNote(NoteFreq(0) * 2f, 1.2f, 0.8f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { InLevel = 0; Collected = 0; Melody = new int[0]; Changed = null; DoorOpened = null; }
    }

    /// <summary>A floating melody shard. Collected ones stay collected through deaths.</summary>
    public class MelodyShard : MonoBehaviour
    {
        public bool collected;
        public int index;
        Transform _gem;
        Vector3 _base;

        public void Init(Transform gem, int i) { _gem = gem; index = i; _base = transform.position; }

        void Update()
        {
            if (collected) return;
            transform.position = _base + Vector3.up * Mathf.Sin(Time.time * 2.2f + _base.x) * 0.12f;
            if (_gem != null) _gem.Rotate(0f, 120f * Time.deltaTime, 0f, Space.Self);
            // Tall pickup zone: the lantern sits on the route, so walking or jumping past it gathers the shard.
            if (PlayerQuery.Overlaps((Vector2)_base + Vector2.down * 0.2f, new Vector2(1.4f, 3.2f)))
            {
                collected = true;
                UmbraFx.Burst(transform.position, MelodyShards.ShardColor);
                MelodyShards.Collect(index);
                gameObject.SetActive(false);
            }
        }
    }
}
