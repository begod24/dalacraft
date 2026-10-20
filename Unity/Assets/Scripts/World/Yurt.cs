using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    public enum YurtStyle { White, Blue, Red, Khan }

    /// <summary>
    /// A киіз үй built from real blocks, like Minecraft village houses: a round felt wall (kerege), a stepped felt dome
    /// (уық) with an open wooden crown (шаңырақ) of glass, a carved door, carpets on the floor and a working interior —
    /// hearth, kazan with water, bed (корпе) on the tör, sandyq and kebeje. Everything can be used, broken and rebuilt.
    /// </summary>
    public static class Yurt
    {
        public struct Cell { public Vector3Int Off; public BlockType Type; public byte Meta; }

        /// <summary>
        /// Block offsets relative to (centre, floor level): dy = 0 is the ground layer (carpets), dy = 1 the first block above it.
        /// <paramref name="door"/> = horizontal direction (Meta.Dir index) the door faces.
        /// </summary>
        public static List<Cell> Build(YurtStyle style, int door)
        {
            var cells = new List<Cell>();
            bool khan = style == YurtStyle.Khan;
            int R = khan ? 4 : 3, wallH = khan ? 3 : 2;
            // white felt cover (киіз); the ornament lives in one band — red, blue or tekemet — like the tied bands of a real yurt
            var felt = BlockType.WoolWhite;
            var band = style switch { YurtStyle.Blue => BlockType.FeltBlue, YurtStyle.Red => BlockType.Tekemet, YurtStyle.Khan => BlockType.Shyrdak, _ => BlockType.FeltRed };
            int k = (door - 2 + 4) % 4;                     // local door is on -Z (facing 2)

            Vector3Int Rot(int x, int y, int z) => k switch
            {
                0 => new Vector3Int(x, y, z), 1 => new Vector3Int(z, y, -x), 2 => new Vector3Int(-x, y, -z), _ => new Vector3Int(-z, y, x),
            };
            byte RotMeta(byte m) => (byte)((m & ~Meta.FacingMask) | ((Meta.Facing(m) + k) % 4));
            void Put(int x, int y, int z, BlockType b, byte m = 0, bool oriented = false) =>
                cells.Add(new Cell { Off = Rot(x, y, z), Type = b, Meta = oriented ? RotMeta(m) : m });

            bool Inside(int x, int z, float r) => x * x + z * z <= (r + 0.5f) * (r + 0.5f);
            bool Edge(int x, int z, float r) => Inside(x, z, r) && (!Inside(x + 1, z, r) || !Inside(x - 1, z, r) || !Inside(x, z + 1, r) || !Inside(x, z - 1, r));

            for (int x = -R - 1; x <= R + 1; x++)
                for (int z = -R - 1; z <= R + 1; z++)
                {
                    if (!Inside(x, z, R)) continue;
                    bool wall = Edge(x, z, R);
                    // floor: carpets inside, a felt sill under the wall
                    Put(x, 0, z, wall ? BlockType.Planks : (x == 0 && z == 0 ? BlockType.Cobble : BlockType.Tekemet));
                    // wall (kerege) with an ornament band on its top row; interior air up to the dome
                    for (int y = 1; y <= wallH; y++)
                        Put(x, y, z, wall ? (y == wallH ? band : felt) : BlockType.Air);
                }
            // dome (уық): stepped rings, hollow inside
            int y0 = wallH + 1;
            for (int ring = 0; ring < R; ring++)
            {
                float r = R - ring;
                for (int x = -R; x <= R; x++)
                    for (int z = -R; z <= R; z++)
                    {
                        if (!Inside(x, z, r)) continue;
                        bool crown = ring == R - 1;
                        if (Inside(x, z, r - 1) && !crown) { Put(x, y0 + ring, z, BlockType.Air); continue; }
                        if (crown)
                        {
                            // шаңырақ: a wooden crown ring around a glass "sky hole"
                            bool centre = Mathf.Abs(x) + Mathf.Abs(z) <= (khan ? 1 : 0);
                            Put(x, y0 + ring, z, centre ? BlockType.Glass : BlockType.SlabPlanks);
                        }
                        else Put(x, y0 + ring, z, felt);
                    }
            }
            // door in the wall at local -Z (two halves), facing out so it opens inward from the path
            Put(0, 1, -R, BlockType.Door, 0, true);
            Put(0, 2, -R, BlockType.Door, Meta.Upper, true);
            if (wallH > 2) Put(0, 3, -R, band);
            Put(0, 0, -R, BlockType.Planks);                                  // threshold (табалдырық)

            // interior: hearth in the centre, kazan beside it, tör (place of honour) at the back
            Put(0, 1, 0, BlockType.Campfire);
            Put(1, 1, 0, BlockType.Kazan, 2, true);                           // front faces the door side
            int back = R - 1;
            Put(1, 1, back, BlockType.Bed, 3, true);                          // foot at x=1, head toward -X
            Put(0, 1, back, BlockType.Bed, 3 | Meta.Second, true);
            Put(-1, 1, back, BlockType.Sandyq, 2, true);
            Put(-back + (khan ? 0 : 1), 1, 1, BlockType.Kebeje, 1, true);
            if (khan)
            {
                Put(2, 1, back - 1, BlockType.Bed, 3, true);
                Put(1, 1, back - 1, BlockType.Bed, 3 | Meta.Second, true);
                Put(-2, 1, -1, BlockType.Sandyq, 1, true);
                Put(2, 1, -2, BlockType.Workbench, 3, true);
            }
            return cells;
        }
    }
}
