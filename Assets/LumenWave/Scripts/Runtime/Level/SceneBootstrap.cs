using System;
using System.Collections;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// The only component placed in generated scenes. Decides what the scene is (title, end, level N)
    /// and lets subsystems build the rest at runtime from code + data.
    /// </summary>
    public class SceneBootstrap : MonoBehaviour
    {
        public string kind = "level";   // "title", "end", "level"
        public int levelNumber = 1;

        /// <summary>Raised for every scene before content is built: (kind, level). Visual rigs, UI, audio hook in here.</summary>
        public static event Action<string, int> Booting;

        /// <summary>Set by SceneFlow when a level has no scene of its own and is loaded into another level's shell.</summary>
        public static int PendingLevelOverride;
        /// <summary>Debug/showcase: custom level data to boot in the next level scene instead of the scene's level.</summary>
        public static LevelData PendingLevelData;

        void Awake()
        {
            int n = levelNumber;
            if (kind == "level" && PendingLevelOverride > 0) { n = PendingLevelOverride; PendingLevelOverride = 0; }
            if (kind == "level" && PendingLevelData != null) { var d = PendingLevelData; PendingLevelData = null; BootLevel(d, n); return; }
            if (kind == "level") BootLevel(n);
            else Boot(kind, 0);
        }

        /// <summary>Raises Booting for a non-level scene (title/end) — also usable from tests.</summary>
        public static void Boot(string kind, int level)
        {
            var _ = GameSystems.Root;
            Booting?.Invoke(kind, level);
        }

        /// <summary>Boots a level without needing a scene file (used by scenes and PlayMode tests).</summary>
        public static LevelRuntime BootLevel(int levelNumber) => BootLevel(LevelDatabase.Load(levelNumber), levelNumber);

        /// <summary>The level data being booted (valid during <see cref="Booting"/> for level scenes).</summary>
        public static LevelData BootingData { get; private set; }

        public static LevelRuntime BootLevel(LevelData data, int levelNumber)
        {
            BootingData = data;
            Boot("level", levelNumber);
            var go = new GameObject("LevelRuntime");
            var rt = go.AddComponent<LevelRuntime>();
            rt.Load(data, levelNumber);
            return rt;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Booting = null; }
    }
}
