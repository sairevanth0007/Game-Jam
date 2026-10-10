using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LumenWave.EditorTools
{
    /// <summary>
    /// Generates every scene from data and builds the Windows player.
    /// Scenes contain only a <see cref="SceneBootstrap"/>; all content is built at runtime from
    /// Resources/Levels/*.txt, so the scenes are fully reproducible.
    /// </summary>
    public static class BuildScript
    {
        public const string SceneDir = "Assets/LumenWave/Scenes";
        public const string BuildPath = "Builds/Windows/LumenWave.exe";

        [MenuItem("LumenWave/Build All Levels")]
        public static void BuildAllLevels()
        {
            ProjectSetup.EnsurePipeline();
            MaterialAssets.EnsureAll();
            Directory.CreateDirectory(SceneDir);
            var scenes = new List<string>();
            scenes.Add(MakeScene(SceneFlow.TitleScene, "title", 0));
            int n = LevelDatabase.Count;
            for (int i = 1; i <= n; i++) scenes.Add(MakeScene(SceneFlow.LevelScene(i), "level", i));
            scenes.Add(MakeScene(SceneFlow.EndScene, "end", 0));
            EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[LumenWave] Built {scenes.Count} scenes ({n} levels): " + string.Join(", ", LevelDatabase.Names));
        }

        static string MakeScene(string name, string kind, int level)
        {
            var path = $"{SceneDir}/{name}.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("SceneBootstrap");
            var b = go.AddComponent<SceneBootstrap>();
            b.kind = kind;
            b.levelNumber = level;
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        [MenuItem("LumenWave/Build Windows Player")]
        public static bool BuildWindows()
        {
            BuildAllLevels();
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[LumenWave] Build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:F0}s -> {BuildPath}");
            return s.result == BuildResult.Succeeded;
        }

        public static void BuildWindowsBatch()
        {
            bool ok = false;
            try { ok = BuildWindows(); }
            catch (System.Exception e) { Debug.LogError("[LumenWave] Build exception: " + e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void BuildAllLevelsBatch()
        {
            try { BuildAllLevels(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError("[LumenWave] " + e); EditorApplication.Exit(1); }
        }
    }
}
