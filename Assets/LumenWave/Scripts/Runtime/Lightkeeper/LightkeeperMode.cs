using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// LIGHTKEEPER MODE: a reactive bot plays Umbra, the human controls four lamps above the arena and tries to
    /// stop her for 90 s (or kill her 3 times). The arena level file is not in index.txt, so the campaign is unaffected.
    /// Any level whose id is <see cref="ArenaName"/> boots in this mode (title button, tests, screenshot run).
    /// </summary>
    public static class LightkeeperMode
    {
        public const string ArenaName = "lightkeeper_arena";
        public const float RoundSeconds = 90f;
        public const int DeathsToWin = 3;

        /// <summary>True while a Lightkeeper round is running (a controller exists).</summary>
        public static bool Active => LightkeeperController.Instance != null;

        /// <summary>Loads a level file by resource name (Resources/Levels/&lt;name&gt;.txt).</summary>
        public static LevelData LoadByName(string name) => LevelData.Load(name);

        /// <summary>Starts the arena from any scene: boots the Level01 scene shell with the arena data.</summary>
        public static void Start()
        {
            GameSession.NewRun();
            SceneBootstrap.PendingLevelData = LoadByName(ArenaName);
            SceneFlow.LoadLevel(1);
        }

        public static void ExitToTitle()
        {
            GameSession.BotMode = false;
            SceneFlow.LoadTitle();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            LevelRuntime.Loaded -= OnLoaded;
            LevelRuntime.Loaded += OnLoaded;
        }

        static void OnLoaded(LevelRuntime rt)
        {
            if (rt == null || rt.Data == null || rt.Data.id != ArenaName) return;
            if (rt.GetComponent<LightkeeperController>() == null) rt.gameObject.AddComponent<LightkeeperController>();
        }
    }
}
