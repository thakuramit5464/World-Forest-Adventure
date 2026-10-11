using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>Architectural pieces shared by buildings and props (roofs, windows, doors).</summary>
    public static class BuildingParts
    {
        /// <summary>
        /// Gable roof with real thickness, ridge along X. Walls top out at <paramref name="eaveY"/>;
        /// the roof overhangs by <paramref name="overhang"/> at the eaves and <paramref name="gableOverhang"/> at the gables.
        /// Returns the ridge height.
        /// </summary>
        public static float GableRoof(MeshBuilder mb, Vector3 centre, float width, float depth, float eaveY, float rise,
            float overhang, float gableOverhang, float thickness, VRamp ramp, int shade)
        {
            float ridgeY = eaveY + rise;
            float halfDepth = depth * 0.5f;
            float eaveZ = halfDepth + overhang;
            float eaveDrop = overhang * rise / halfDepth;
            float slabWidth = width + gableOverhang * 2f;

            foreach (float side in new[] { 1f, -1f })
            {
                var ridge = new Vector3(0f, ridgeY, 0f);
                var eave = new Vector3(0f, eaveY - eaveDrop, eaveZ * side);
                Vector3 dir = (eave - ridge).normalized;
                Vector3 normal = Vector3.Cross(Vector3.right, dir);
                if (normal.y < 0f) normal = -normal;
                Vector3 mid = (ridge + eave) * 0.5f + normal * thickness * 0.5f;
                Shapes.Box(mb, centre + mid, new Vector3(slabWidth, thickness, Vector3.Distance(ridge, eave)),
                    Quaternion.LookRotation(dir, normal), n => VillagePalette.UV(ramp, shade + (n.y > 0.3f ? 0 : -2)));
            }
            // Ridge cap.
            Shapes.Box(mb, centre + new Vector3(0f, ridgeY + thickness * 0.75f, 0f), new Vector3(slabWidth + 0.06f, 0.14f, 0.3f),
                GenUtil.VillageFlat(ramp, shade - 1));
            return ridgeY;
        }

        /// <summary>Triangular gable wall at x = ±width/2 filling the space under the roof.</summary>
        public static void Gables(MeshBuilder mb, Vector3 centre, float width, float depth, float eaveY, float ridgeY, VRamp ramp, int shade)
        {
            foreach (float side in new[] { 1f, -1f })
            {
                float x = side * width * 0.5f;
                mb.Triangle(centre + new Vector3(x, eaveY, -depth * 0.5f), centre + new Vector3(x, eaveY, depth * 0.5f),
                    centre + new Vector3(x, ridgeY, 0f), new Vector3(side, 0f, 0f), VillagePalette.UV(ramp, shade));
            }
        }

        /// <summary>
        /// Window on a wall. <paramref name="facing"/> maps local +Z to the wall's outward normal;
        /// <paramref name="centre"/> is on the wall surface.
        /// </summary>
        public static void Window(MeshBuilder mb, System.Random rng, Vector3 centre, Quaternion facing, float width, float height,
            VRamp? shutters, bool flowerBox)
        {
            Vector3 P(float x, float y, float z) => centre + facing * new Vector3(x, y, z);
            Shapes.Box(mb, P(0f, 0f, 0.04f), new Vector3(width + 0.16f, height + 0.16f, 0.08f), facing, GenUtil.VillageFlat(VRamp.TimberDark, 3));
            Shapes.Box(mb, P(0f, 0f, 0.07f), new Vector3(width, height, 0.04f), facing, GenUtil.VillageFlat(VRamp.Window, 5));
            Shapes.Box(mb, P(0f, 0f, 0.095f), new Vector3(0.05f, height, 0.03f), facing, GenUtil.VillageFlat(VRamp.TimberDark, 3));
            Shapes.Box(mb, P(0f, 0f, 0.095f), new Vector3(width, 0.05f, 0.03f), facing, GenUtil.VillageFlat(VRamp.TimberDark, 3));
            Shapes.Box(mb, P(0f, -(height * 0.5f + 0.1f), 0.1f), new Vector3(width + 0.3f, 0.07f, 0.2f), facing, GenUtil.Village(VRamp.Wood, 4));

            if (shutters.HasValue)
            {
                foreach (float side in new[] { -1f, 1f })
                    Shapes.Box(mb, P(side * (width * 0.5f + 0.27f), 0f, 0.06f), new Vector3(0.36f, height + 0.1f, 0.05f), facing,
                        GenUtil.VillageFlat(shutters.Value, 4));
            }

            if (flowerBox)
            {
                Shapes.Box(mb, P(0f, -(height * 0.5f + 0.28f), 0.18f), new Vector3(width + 0.1f, 0.22f, 0.24f), facing, GenUtil.Village(VRamp.Wood, 3));
                for (int i = 0; i < 4; i++)
                {
                    float x = (i - 1.5f) * (width / 4f);
                    VRamp ramp = i % 2 == 0 ? VRamp.Accent : VRamp.Window;
                    Shapes.Blob(mb, rng, P(x, -(height * 0.5f + 0.12f), 0.2f), 0.09f, Vector3.one, 0.15f, 0, _ => VillagePalette.UV(ramp, 5));
                }
            }
        }

        /// <summary>Framed plank door with handle on the front wall (z = <paramref name="wallZ"/>).</summary>
        public static void Door(MeshBuilder mb, float x, float floorY, float wallZ, float width, float height)
        {
            Shapes.Box(mb, new Vector3(x, floorY + height * 0.5f + 0.06f, wallZ + 0.05f), new Vector3(width + 0.2f, height + 0.12f, 0.1f),
                GenUtil.VillageFlat(VRamp.TimberDark, 2));
            Shapes.Box(mb, new Vector3(x, floorY + height * 0.5f, wallZ + 0.09f), new Vector3(width, height, 0.06f), GenUtil.VillageFlat(VRamp.Wood, 3));
            foreach (float px in new[] { -width / 3f, width / 3f })
                Shapes.Box(mb, new Vector3(x + px, floorY + height * 0.5f, wallZ + 0.125f), new Vector3(0.03f, height * 0.95f, 0.02f),
                    GenUtil.VillageFlat(VRamp.Wood, 1));
            Shapes.Box(mb, new Vector3(x + width * 0.33f, floorY + height * 0.5f, wallZ + 0.14f), new Vector3(0.07f, 0.07f, 0.05f),
                GenUtil.VillageFlat(VRamp.Window, 6));
        }
    }
}
