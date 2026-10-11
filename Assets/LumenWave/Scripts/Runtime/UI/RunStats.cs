using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Persistent records: best full-run time (only for runs started at level 1), per-level best time
    /// and the most secrets found per level. Also counts every secret orb across the shipped levels.
    /// </summary>
    public static class RunStats
    {
        const string BestRunKey = "lw.best.run";
        static string BestLevelKey(int n) => "lw.best.L" + n;
        static string SecretsLevelKey(int n) => "lw.secrets.L" + n;

        /// <summary>Level the current run started on (1 = a full run that may set a record).</summary>
        public static int RunStartLevel = 1;
        /// <summary>True when the run that just ended beat the previous best.</summary>
        public static bool LastRunWasNewBest;

        static int[] _orbsPerLevel;

        public static float BestRun => PlayerPrefs.GetFloat(BestRunKey, 0f);
        public static float BestLevel(int n) => PlayerPrefs.GetFloat(BestLevelKey(n), 0f);
        public static int SecretsFound(int n) => PlayerPrefs.GetInt(SecretsLevelKey(n), 0);

        /// <summary>Secret orbs in level n (from the level data).</summary>
        public static int SecretsIn(int n)
        {
            if (_orbsPerLevel == null)
            {
                int count = 0;
                try { count = LevelDatabase.Count; } catch { }
                _orbsPerLevel = new int[count + 1];
                for (int i = 1; i <= count; i++)
                {
                    try { _orbsPerLevel[i] = LevelDatabase.Load(i).entities.FindAll(e => e.type == "orb").Count; }
                    catch { _orbsPerLevel[i] = 0; }
                }
            }
            return n > 0 && n < _orbsPerLevel.Length ? _orbsPerLevel[n] : 0;
        }

        public static int TotalSecrets
        {
            get
            {
                SecretsIn(1);
                int t = 0;
                for (int i = 1; i < _orbsPerLevel.Length; i++) t += _orbsPerLevel[i];
                return t;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.GameWon -= OnGameWon;
            GameEvents.GameWon += OnGameWon;
        }

        static void OnLevelStarted(int n)
        {
            if (GameSession.TotalTime < 0.5f) { RunStartLevel = n; LastRunWasNewBest = false; }
        }

        static void OnLevelCompleted(int n, float time)
        {
            if (GameSession.BotMode) return;
            float best = BestLevel(n);
            if (best <= 0f || time < best) PlayerPrefs.SetFloat(BestLevelKey(n), time);
            if (Secrets.CollectedInLevel > SecretsFound(n)) PlayerPrefs.SetInt(SecretsLevelKey(n), Secrets.CollectedInLevel);
            PlayerPrefs.Save();
        }

        static void OnGameWon()
        {
            LastRunWasNewBest = false;
            if (GameSession.BotMode || RunStartLevel != 1) return;
            float best = BestRun;
            if (best <= 0f || GameSession.TotalTime < best)
            {
                PlayerPrefs.SetFloat(BestRunKey, GameSession.TotalTime);
                PlayerPrefs.Save();
                LastRunWasNewBest = true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _orbsPerLevel = null; RunStartLevel = 1; LastRunWasNewBest = false; }
    }
}
