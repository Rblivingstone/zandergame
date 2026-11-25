using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace ZanderGame.EditorTools
{
    /// <summary>
    /// One-click builds.
    ///   Menu:  Zander > Build for Windows (one click)
    ///   CLI:   Unity.exe -batchmode -quit -projectPath &lt;project&gt;
    ///                    -executeMethod ZanderGame.EditorTools.ZanderBuild.BuildWindowsCI
    ///          (build_windows.bat in the repo root does this for you)
    /// Output: Builds/Windows/ZanderHoopShrines.exe and Builds/ZanderHoopShrines-Windows.zip
    /// </summary>
    public static class ZanderBuild
    {
        const string Product = "Zander's Hoop Shrines";
        const string ExeName = "ZanderHoopShrines.exe";
        const string IconPath = "Assets/ZanderGame/Icon/ZanderIcon.png";

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        static string OutDir => Path.Combine(ProjectRoot, "Builds", "Windows");
        static string ZipPath => Path.Combine(ProjectRoot, "Builds", "ZanderHoopShrines-Windows.zip");

        [MenuItem("Zander/Build for Windows (one click)", priority = 1)]
        public static void BuildWindowsMenu()
        {
            var ok = BuildWindows(out string message);
            if (ok)
            {
                EditorUtility.RevealInFinder(Path.Combine(OutDir, ExeName));
                EditorUtility.DisplayDialog(Product, "Build finished!\n\n" + message +
                    "\n\nCopy the Windows folder (or the zip) to any Windows 10/11 PC, plug in the controller and double-click " +
                    ExeName + ".", "Awesome");
            }
            else EditorUtility.DisplayDialog(Product, "The build failed:\n\n" + message, "OK");
        }

        [MenuItem("Zander/Build and Run on this PC", priority = 2)]
        public static void BuildAndRun()
        {
            if (BuildWindows(out string message))
                System.Diagnostics.Process.Start(Path.Combine(OutDir, ExeName));
            else EditorUtility.DisplayDialog(Product, "The build failed:\n\n" + message, "OK");
        }

        /// <summary>Entry point for command-line / batch builds.</summary>
        public static void BuildWindowsCI()
        {
            bool ok = BuildWindows(out string message);
            Debug.Log("[ZanderBuild] " + message);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool BuildWindows(out string message)
        {
            try
            {
                ZanderProjectSetup.PrepareForBuild();
                ConfigurePlayer();
                Directory.CreateDirectory(OutDir);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ZanderProjectSetup.ScenePath },
                    locationPathName = Path.Combine(OutDir, ExeName),
                    target = BuildTarget.StandaloneWindows64,
#if UNITY_2021_2_OR_NEWER
                    targetGroup = BuildTargetGroup.Standalone,
#endif
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    message = "Result: " + report.summary.result + " (" + report.summary.totalErrors +
                              " errors - see the Console).\nIs 'Windows Build Support' installed for this Unity version in Unity Hub?";
                    return false;
                }
                string zipNote = "";
                try
                {
                    if (File.Exists(ZipPath)) File.Delete(ZipPath);
                    System.IO.Compression.ZipFile.CreateFromDirectory(OutDir, ZipPath);
                    zipNote = "\nZip: " + ZipPath;
                }
                catch (Exception e)
                {
                    zipNote = "\n(Could not create the zip: " + e.Message + ")";
                }
                message = "Game: " + Path.Combine(OutDir, ExeName) + zipNote +
                          string.Format("\nSize: {0:0.0} MB", report.summary.totalSize / (1024.0 * 1024.0));
                return true;
            }
            catch (Exception e)
            {
                message = e.Message;
                return false;
            }
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = Product;
            PlayerSettings.companyName = "Zander Games";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
#endif
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
#if UNITY_2021_2_OR_NEWER
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
#else
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
#endif
            }
        }
    }
}
