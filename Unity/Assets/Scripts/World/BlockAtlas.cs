using UnityEngine;

namespace Dala
{
    /// <summary>
    /// The block textures: several 8x8 atlas pages from Blender (Blender/tools/block_atlas.py), stacked into one
    /// Texture2DArray so the whole world still draws with a single material. Tile index = page * 64 + slot.
    /// </summary>
    public static class BlockAtlas
    {
        /// <summary>Page order = array slice. New pages are appended here and in block_atlas.py.</summary>
        public static readonly string[] Pages = { "T_BlockAtlas", "T_BlockAtlas_Trees", "T_BlockAtlas_Build" };

        static Texture2DArray array;
        static Texture2D[] pageTextures;

        public static Texture2DArray Array
        {
            get
            {
                if (array == null) Build();
                return array;
            }
        }

        /// <summary>Drops the cached array so it is rebuilt from the (re-imported) page textures — editor previews.</summary>
        public static void Reset() { array = null; pageTextures = null; }

        /// <summary>The page texture holding <paramref name="tile"/> (for UI sprites).</summary>
        public static Texture2D PageOf(int tile)
        {
            if (pageTextures == null) Build();
            int p = Mathf.Clamp(tile / BlockInfo.TilesPerPage, 0, pageTextures.Length - 1);
            return pageTextures[p];
        }

        static void Build()
        {
            pageTextures = new Texture2D[Pages.Length];
            for (int i = 0; i < Pages.Length; i++) pageTextures[i] = Resources.Load<Texture2D>("Textures/" + Pages[i]);
            var first = pageTextures[0];
            int w = first != null ? first.width : 128, h = first != null ? first.height : 128;
            array = new Texture2DArray(w, h, Pages.Length, TextureFormat.RGBA32, false)
            {
                name = "BlockAtlasPages", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0,
            };
            var clear = new Color32[w * h];
            for (int i = 0; i < Pages.Length; i++)
            {
                var t = pageTextures[i];
                if (t != null && t.isReadable && t.width == w && t.height == h) array.SetPixels32(t.GetPixels32(), i);
                else
                {
                    if (t != null) Debug.LogWarning($"Block atlas page {Pages[i]} must be readable and {w}x{h}");
                    array.SetPixels32(clear, i);
                }
            }
            array.Apply(false, true);
        }

        /// <summary>A Dala/VoxelLit material sampling the atlas pages.</summary>
        public static Material CreateMaterial(string name)
        {
            var m = new Material(Shader.Find("Dala/VoxelLit")) { name = name };
            m.SetTexture("_BaseMap", Array);
            return m;
        }
    }
}
