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
        public const string ScenePath = "Assets/Scenes/Main.unity";

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
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;   // user cancelled
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Zander/Reimport Blender Models")]
        public static void Reimport()
        {
            AssetDatabase.ImportAsset("Assets/ZanderGame/Resources/Models",
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Everything a build needs: scene, shaders, input handling.</summary>
        public static void PrepareForBuild()
        {
            Setup(false);
        }

        /// <summary>The game reads the controller through the classic Input Manager
        /// (axes in ProjectSettings/InputManager.asset). If the project was switched to
        /// "Input System Package (New)" only, switch it to "Both".</summary>
        static void EnsureInputHandling()
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (objs == null || objs.Length == 0) return;
            var so = new SerializedObject(objs[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue != 1) return;   // 0 = old, 1 = new only, 2 = both
            prop.intValue = 2;
            so.ApplyModifiedProperties();
            Debug.LogWarning("[Zander] Active Input Handling set to 'Both' so the controller works. Restart Unity to apply.");
        }

        static void Setup(bool verbose)
        {
            EnsureInputHandling();
            foreach (var s in new[] { "Standard", "ZanderGame/Toon", "Sprites/Default", "Particles/Standard Unlit" })
                AlwaysInclude(s);
            EnsureScene();
            ConfigureIcon();
            if (verbose)
                EditorUtility.DisplayDialog("Zander's Hoop Shrines",
                    "Project ready!\n\nOpen Assets/Scenes/Main.unity and press Play.\nTo make a game for Windows: Zander > Build for Windows.",
                    "Let's go!");
        }

        static void ConfigureIcon()
        {
            const string path = "Assets/ZanderGame/Icon/ZanderIcon.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null || ti.textureType == TextureImporterType.Default && ti.isReadable) return;
            ti.textureType = TextureImporterType.Default;
            ti.isReadable = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
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
                // Write an empty scene file straight to disk. (Creating it through the
                // SceneManager fails while an unsaved "Untitled" scene is open.)
                // The game builds its own camera, lights and world when you press Play.
                Directory.CreateDirectory("Assets/Scenes");
                File.WriteAllText(ScenePath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
                AssetDatabase.ImportAsset(ScenePath);
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
