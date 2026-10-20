using UnityEditor;
using UnityEngine;

namespace Dala.EditorTools
{
    /// <summary>
    /// Import settings for assets exported from Blender (Blender/kazakh_mine_assets.blend):
    /// pixel-art textures stay crisp, animation clips loop, imported materials are matte.
    /// </summary>
    public class BlenderAssetImportRules : AssetPostprocessor
    {
        const string Models = "Assets/Resources/Models/";
        const string Textures = "Assets/Resources/Textures/";
        static readonly string[] OneShotClips = { "Mine" };

        public override uint GetVersion() => 4;

        const string UI = "Assets/Resources/UI/";

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(UI) || assetPath.StartsWith(Textures + "Icons/"))
            {
                var si = (TextureImporter)assetImporter;
                si.textureType = TextureImporterType.Sprite;
                si.spriteImportMode = SpriteImportMode.Single;
                si.filterMode = FilterMode.Point;
                si.mipmapEnabled = false;
                si.textureCompression = TextureImporterCompression.Uncompressed;
                si.alphaIsTransparency = true;
                si.spritePixelsPerUnit = 16;
                string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                // 9-slice borders (left, bottom, right, top)
                si.spriteBorder = n.StartsWith("UI_Panel") ? new Vector4(12, 12, 12, 12)
                    : n.StartsWith("UI_Button") ? new Vector4(6, 6, 6, 6)
                    : n == "UI_SliderBg" ? new Vector4(3, 3, 3, 3)
                    : n == "UI_Slot" ? new Vector4(3, 3, 3, 3)
                    : Vector4.zero;
                return;
            }
            if (!assetPath.StartsWith(Textures)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Models)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = assetPath.Contains("/Foliage/")
                ? ModelImporterMaterialImportMode.None
                : ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Models)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                bool oneShot = System.Array.Exists(OneShotClips, n => c.name.EndsWith(n));
                c.loopTime = !oneShot;
                c.loopPose = !oneShot;
            }
            mi.clipAnimations = clips;
        }

        void OnPostprocessMaterial(Material m)
        {
            if (!assetPath.StartsWith(Models)) return;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
        }
    }
}
