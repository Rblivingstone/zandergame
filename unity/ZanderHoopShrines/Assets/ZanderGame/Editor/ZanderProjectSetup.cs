using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZanderGame.EditorTools
{
    /// <summary>
    /// One-click project setup. Runs automatically when the project opens:
    ///  * makes sure the shaders the game creates at runtime are included in builds
    ///  * creates Assets/Scenes/Main.unity and adds it to Build Settings
    /// Also available from the menu: Zander > Setup Project.
    /// </summary>
    [InitializeOnLoad]
    public static class ZanderProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static ZanderProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("ZanderSetupDone", false))
                {
                    SessionState.SetBool("ZanderSetupDone", true);
                    Setup(false);
                }
            };
        }

        [MenuItem("Zander/Setup Project")]
        public static void SetupMenu() => Setup(true);

        [MenuItem("Zander/Open Main Scene")]
        public static void OpenMain()
        {
            Setup(false);
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Zander/Reimport Blender Models")]
        public static void Reimport()
        {
            AssetDatabase.ImportAsset("Assets/ZanderGame/Resources/Models",
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        static void Setup(bool verbose)
        {
            foreach (var s in new[] { "Standard", "ZanderGame/Toon", "Sprites/Default", "Particles/Standard Unlit" })
                AlwaysInclude(s);
            EnsureScene();
            if (verbose)
                EditorUtility.DisplayDialog("Zander's Hoop Shrines",
                    "Project ready!\n\nOpen Assets/Scenes/Main.unity and press Play.", "Let's go!");
        }

        static void AlwaysInclude(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) return;
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (objs == null || objs.Length == 0) return;
            var so = new SerializedObject(objs[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                var note = new GameObject("README - just press Play (the game builds itself at runtime)");
                note.transform.position = Vector3.zero;
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.Refresh();
            }
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == ScenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            list.Sort((a, b) => a.path == ScenePath ? -1 : b.path == ScenePath ? 1 : 0);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
