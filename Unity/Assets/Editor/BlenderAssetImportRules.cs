using UnityEditor;
using UnityEngine;

namespace Dala.EditorTools
{
    /// <summary>
    /// Import settings for assets exported from Blender (Blender/DalaCraft_Assets.blend):
    /// pixel-art textures stay crisp, animation clips loop, imported materials are matte.
    /// </summary>
    public class BlenderAssetImportRules : AssetPostprocessor
    {
        const string Models = "Assets/Resources/Models/";
        const string Textures = "Assets/Resources/Textures/";
        static readonly string[] OneShotClips = { "Mine" };

        public override uint GetVersion() => 7;

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
            // block atlas pages are copied into a Texture2DArray at runtime (BlockAtlas) — CPU-readable, plain RGBA32
            if (System.IO.Path.GetFileName(assetPath).StartsWith("T_BlockAtlas"))
            {
                ti.isReadable = true;
                var ps = ti.GetDefaultPlatformTextureSettings();
                ps.format = TextureImporterFormat.RGBA32;
                ti.SetPlatformTextureSettings(ps);
            }
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
            mi.materialImportMode = assetPath.Contains("/Foliage/") || assetPath.Contains("/Blocks/")
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
            LinkPalette(m);
        }

        // Runs for every material created via MaterialDescription (OnPostprocessMaterial may not).
        void OnPreprocessMaterialDescription(UnityEditor.AssetImporters.MaterialDescription description, Material m, AnimationClip[] clips)
        {
            if (!assetPath.StartsWith(Models)) return;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            LinkPalette(m, description.materialName);
        }

        /// <summary>
        /// Blender material M_&lt;Name&gt; paints from T_Palette_&lt;Name&gt; (Resources/Textures). FBX exports strip paths and the
        /// texture folder is not above the model, so Unity's search misses it — link it by name instead.
        /// </summary>
        static void LinkPalette(Material m, string name = null)
        {
            name ??= m.name;
            if (!m.HasProperty("_BaseMap") || m.GetTexture("_BaseMap") != null || !name.StartsWith("M_")) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "T_Palette_" + name.Substring(2) + ".png");
            if (tex == null) return;
            m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", Color.white);
        }

        /// <summary>A palette imported after its model (same export batch) → reimport the model so its material picks it up.</summary>
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!path.StartsWith(Textures + "T_Palette_") || !path.EndsWith(".png")) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path).Substring("T_Palette_".Length);
                foreach (var guid in AssetDatabase.FindAssets("SM_" + name + " t:Model", new[] { "Assets/Resources/Models" }))
                {
                    var model = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(model) != "SM_" + name) continue;
                    if (System.Array.IndexOf(imported, model) >= 0 && AssetDatabase.LoadAssetAtPath<GameObject>(model) != null
                        && HasTexture(model)) continue;
                    AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        static bool HasTexture(string model)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(model))
                if (o is Material mat && mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null) return false;
            return true;
        }
    }
}
