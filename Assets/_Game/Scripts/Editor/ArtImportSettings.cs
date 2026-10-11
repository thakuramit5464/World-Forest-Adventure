using System.IO;
using UnityEditor;

namespace Greenhaven.Editor
{
    /// <summary>
    /// Import rules for third-party art under Assets/_Game/Art. Keeps settings reproducible
    /// and mobile-friendly. Bump <see cref="GetVersion"/> after changing a rule so Unity reimports.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        public const string CharactersFolder = "Assets/_Game/Art/Characters/Adventurer/";
        public const string AnimationLibraryFolder = "Assets/_Game/Art/Animations/UniversalAnimationLibrary/";
        public const string NatureFolder = "Assets/_Game/Art/Nature/";
        public const string AnimalsFolder = "Assets/_Game/Art/Animals/";

        public override uint GetVersion() => 3;

        void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            if (!assetPath.StartsWith("Assets/_Game/Art/")) return;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;

            if (assetPath.StartsWith(CharactersFolder))
            {
                // Humanoid so the Universal Animation Library clips retarget onto the Kenney rig.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.None; // skins assigned by the builder
                importer.importAnimation = false;
            }
            else if (assetPath.StartsWith(AnimationLibraryFolder))
            {
                // Per the pack's Unity setup sheet: Humanoid + Bake Axis Conversion.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = true;
            }
            else if (assetPath.StartsWith(NatureFolder))
            {
                // Static props. Materials are remapped to M_Nature_* by the scene builder.
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            }
            else if (assetPath.StartsWith(AnimalsFolder))
            {
                // Simple vertex-coloured animals with embedded colour materials.
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.importAnimation = true;
            }
        }

        void OnPreprocessAnimation()
        {
            bool library = assetPath.StartsWith(AnimationLibraryFolder);
            bool animals = assetPath.StartsWith(AnimalsFolder);
            if (!library && !animals) return;

            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                // "Rig|Walk_Loop" → "Walk_Loop", "Armature|Idle" → "Idle".
                int bar = clip.name.LastIndexOf('|');
                if (bar >= 0) clip.name = clip.name.Substring(bar + 1);

                clip.loopTime = library
                    ? clip.name.EndsWith("_Loop")
                    : clip.name == "Idle" || clip.name.StartsWith("Walk") || clip.name == "Run";

                // Root motion is not used (CharacterController moves the player): bake it into the pose.
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Game/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 512; // stylised flat textures; plenty on phones
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;

            if (assetPath.StartsWith(NatureFolder))
            {
                // Leaf/grass/flower cards use alpha cutout; dilate colour into transparent areas.
                string file = Path.GetFileNameWithoutExtension(assetPath);
                bool cutout = file.StartsWith("Leaf") || file.StartsWith("Leaves") || file == "Grass" || file == "Flowers";
                importer.alphaIsTransparency = cutout;
                importer.mipMapsPreserveCoverage = cutout;
                if (cutout) importer.alphaTestReferenceValue = 0.5f;
            }
        }
    }
}
