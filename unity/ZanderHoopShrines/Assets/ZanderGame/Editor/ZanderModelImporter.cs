using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZanderGame.EditorTools
{
    /// <summary>
    /// Import settings for the Blender-generated FBX files in ZanderGame/Resources/Models:
    /// metre scale, axis conversion baked in, Generic rig, embedded textures/materials
    /// and looping flags on the looping animation clips.
    /// </summary>
    public class ZanderModelImporter : AssetPostprocessor
    {
        static bool IsOurs(string path) =>
            path.Replace('\\', '/').Contains("ZanderGame/Resources/Models/") &&
            path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsOurs(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            bool animated = assetPath.Contains("Zander.fbx") || assetPath.Contains("KingSwat.fbx");
            mi.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            mi.importAnimation = animated;
            mi.avatarSetup = animated ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
        }

        void OnPreprocessAnimation()
        {
            if (!IsOurs(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var c in clips)
            {
                string n = c.name.Split('|').Last().Trim().ToLowerInvariant();
                c.loopTime = ClipAnimator.LoopingNames.Contains(n);
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
    }
}
