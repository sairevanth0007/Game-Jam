using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LumenWave.EditorTools
{
    /// <summary>Editor menu: builds and saves the one scene the game needs.</summary>
    public static class LumenWaveMenu
    {
        public const string ScenePath = "Assets/Scenes/LumenWave.unity";

        [MenuItem("LumenWave/Create Game Scene", priority = 0)]
        public static void CreateGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("LumenWave", $"{ScenePath} already exists. Replace it?", "Replace", "Cancel"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.AutoAssignAssets();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[LumenWave] Could not save " + ScenePath);
                return;
            }

            // Scene 0 in Build Settings; keep the others after it.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Selection.activeGameObject = go;
            Debug.Log($"[LumenWave] Created {ScenePath} with GameBootstrap and set it as Build Settings scene 0. Press Play.");
        }

        [MenuItem("LumenWave/Refresh Level List", priority = 1)]
        public static void RefreshLevels()
        {
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[LumenWave] No GameBootstrap in the open scene.");
                return;
            }
            bootstrap.AutoAssignAssets();
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            Debug.Log($"[LumenWave] Level list refreshed: {GameBootstrap.FindLevelFiles().Length} file(s) in {GameBootstrap.LevelsFolder}. Save the scene.");
        }
    }
}
