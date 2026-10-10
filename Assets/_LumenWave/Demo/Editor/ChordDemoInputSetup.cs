using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LumenWave.Demo.Editor
{
    public static class ChordDemoInputSetup
    {
        public const string ActionsPath="Assets/InputSystem_Actions.inputactions";
        public static InputActionAsset GetActions()
        {
            // Preserve the existing Player/UI maps and all user-authored bindings.
            var asset=InputActionAsset.FromJson(File.ReadAllText(ActionsPath));
            var map=asset.FindActionMap(ChordInput.MapName) ?? asset.AddActionMap(ChordInput.MapName);
            bool changed=false;
            string[] oldNames={"LeftC","LeftE","RightC","RightE","G"};
            string[] movementNames={"LeftFirst","LeftSecond","RightFirst","RightSecond","Jump"};
            for (int i=0; i<oldNames.Length; i++)
            {
                var old=map.FindAction(oldNames[i]);
                if (old!=null && map.FindAction(movementNames[i])==null)
                { old.Rename(movementNames[i]); changed=true; }
            }
            string[] names={"LeftFirst","LeftSecond","RightFirst","RightSecond","Jump","Restart","NoteC","NoteE","NoteG"};
            string[] keys={"q","w","o","p","space","r","j","k","l"};
            for (int i=0; i<names.Length; i++)
            {
                if (map.FindAction(names[i])!=null) continue;
                var action=map.AddAction(names[i],InputActionType.Button,expectedControlLayout:"Button");
                action.AddBinding("<Keyboard>/"+keys[i],groups:"Keyboard&Mouse");
                changed=true;
            }
            if (changed)
            {
                File.WriteAllText(ActionsPath,asset.ToJson());
                AssetDatabase.ImportAsset(ActionsPath,ImportAssetOptions.ForceUpdate);
            }
            UnityEngine.Object.DestroyImmediate(asset);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        }
        [MenuItem("Tools/LumenWave/Connect Demo Input Actions")]
        public static void ConnectScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i=0; i<SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits before connecting input actions.");
            var actions=GetActions();
            Scene previous=SceneManager.GetActiveScene();
            foreach (var path in new[] { ChordDemoBuilder.TowerPath,ChordDemoBuilder.LabPath })
            {
                var scene=SceneManager.GetSceneByPath(path);
                bool wasLoaded=scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var input in root.GetComponentsInChildren<ChordInput>()) input.Configure(actions);
                    foreach (var hud in root.GetComponentsInChildren<DemoHud>())
                    {
                        hud.controls=hud.transform.Find("Footer/Controls").GetComponent<TMP_Text>();
                        hud.controls.text=hud.input.ControlsHint;
                    }
                    foreach (var camera in root.GetComponentsInChildren<Camera>()) camera.rect=new Rect(0,0,1,1);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                if (!wasLoaded) EditorSceneManager.CloseScene(scene,true);
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            AssetDatabase.SaveAssets();
            Debug.Log("PASS: ChordDemo action map connected to both demo scenes; original action maps preserved.");
        }
    }
}
