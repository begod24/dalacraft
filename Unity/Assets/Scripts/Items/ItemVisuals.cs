using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dala
{
    /// <summary>
    /// 3D look of any item, shared by dropped items and the first-person hand. All meshes come from Blender:
    /// cube blocks → Models/Blocks/Blocks.fbx (atlas UVs, drawn with the voxel material),
    /// model blocks (workbench, campfire, torch…) → their block-entity model, items → Models/Items/SM_Item_*.
    /// </summary>
    public static class ItemVisuals
    {
        static Dictionary<string, Mesh> blockMeshes;
        static Material blockMat;

        static Mesh BlockMesh(BlockType b)
        {
            if (blockMeshes == null)
            {
                blockMeshes = new Dictionary<string, Mesh>();
                foreach (var m in Resources.LoadAll<Mesh>("Models/Blocks/Blocks")) blockMeshes[m.name.Replace("SM_Block_", "")] = m;
            }
            return blockMeshes.TryGetValue(b.ToString(), out var mesh) ? mesh : null;
        }

        static Material BlockMaterial()
        {
            if (World.Instance != null && World.Instance.VoxelMaterial != null) return World.Instance.VoxelMaterial;
            if (blockMat == null) blockMat = BlockAtlas.CreateMaterial("VoxelItems");
            return blockMat;
        }

        /// <summary>
        /// Builds the item's model under <paramref name="parent"/>, centred on the parent and scaled so its largest side is
        /// <paramref name="size"/> metres. Returns the root (null for an empty/unknown item).
        /// </summary>
        public static GameObject Create(int id, Transform parent, float size, bool castShadows = true)
        {
            var def = Items.Get(id);
            if (def == null) return null;
            GameObject model = null;
            if (def.IsBlock && !BlockInfo.IsEntity(def.Block))
            {
                var mesh = BlockMesh(def.Block);
                if (mesh == null) return null;
                model = new GameObject("Block_" + def.Block);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = BlockMaterial();
            }
            else
            {
                string path = def.IsBlock ? BlockEntities.ModelPathOf(def.Block) : "Items/SM_Item_" + (ItemType)def.Id;
                var prefab = path != null ? Resources.Load<GameObject>("Models/" + path) : null;
                if (prefab == null) return null;
                model = Object.Instantiate(prefab);
            }

            var root = new GameObject("Item_" + def.Name);
            root.transform.SetParent(parent, false);
            model.transform.SetParent(root.transform, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Kill(c);

            // normalise: centre the bounds on the root and fit the largest side to `size`
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float k = size / Mathf.Max(0.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                model.transform.localScale *= k;
                model.transform.position -= (b.center - root.transform.position) * k;
                foreach (var r in rs) r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return root;
        }

        /// <summary>Destroy that also works for editor previews (outside Play Mode).</summary>
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
