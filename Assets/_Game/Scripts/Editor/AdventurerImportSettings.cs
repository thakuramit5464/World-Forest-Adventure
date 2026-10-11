using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor
{
    /// <summary>
    /// Import rules for the adventurer character (Kenney Animated Characters 1, CC0).
    /// Generic rig: all FBX files share one skeleton, so clips bind by bone path.
    /// </summary>
    public sealed class AdventurerImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/_Game/Art/Characters/Adventurer/";

        static bool InFolder(string path) => path.StartsWith(Folder);

        void OnPreprocessModel()
        {
            if (!InFolder(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // materials come from M_Adventurer
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            bool isAnimationFile = assetPath.Contains("/Animations/");
            importer.importAnimation = isAnimationFile;
        }

        void OnPreprocessAnimation()
        {
            if (!InFolder(assetPath) || !assetPath.Contains("/Animations/")) return;
            var importer = (ModelImporter)assetImporter;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool loop = fileName == "idle" || fileName == "run";

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                // One take per file; give it a predictable name for the Animator builder.
                clips[i].name = clips.Length == 1 ? Capitalize(fileName) : $"{Capitalize(fileName)}_{i}";
                clips[i].loopTime = loop;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].lockRootRotation = true;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!InFolder(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 512; // flat-colour skin; 512 is plenty on phones
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }

        static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
