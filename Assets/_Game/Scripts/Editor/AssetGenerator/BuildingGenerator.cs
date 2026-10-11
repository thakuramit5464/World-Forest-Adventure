using System.Collections.Generic;
using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Village buildings as single meshes: stone foundation and step, plaster walls, optional timber
    /// frame, thick gable roof with ridge cap, framed door, windows, chimney. Front (door) faces +Z;
    /// pivot at the centre of the foundation's base. Variation is deliberately limited (roof colour,
    /// plaster shade, framing, door position, shutters) so a street stays coherent.
    /// </summary>
    public sealed class BuildingGenerator : IAssetGenerator
    {
        public struct Dimensions
        {
            public float Width, Depth, WallHeight, RoofRise, Foundation;
        }

        readonly AssetCategory kind;

        public BuildingGenerator(AssetCategory kind) => this.kind = kind;

        public AssetCategory Category => kind;
        public string Prefix => kind.ToString();
        public string DisplayName => kind == AssetCategory.Cottage ? "Cottages" : kind == AssetCategory.House ? "Family Houses" : "Shops / Workshops";
        public string Group => "Buildings";
        public int DefaultCount => kind == AssetCategory.Cottage ? 3 : kind == AssetCategory.House ? 2 : 1;
        public Palette Palette => VillagePalette.Palette;

        static bool Timbered(AssetCategory kind, int variant) => kind == AssetCategory.Shop || variant % 3 != 1;
        static VRamp RoofRamp(int variant) => variant % 2 == 0 ? VRamp.RoofRed : VRamp.RoofBlue;

        public string StyleName(int variant) =>
            $"{(RoofRamp(variant) == VRamp.RoofRed ? "Red" : "Blue")} roof, {(Timbered(kind, variant) ? "timbered" : "plaster")}";

        /// <summary>Nominal footprint, used by the village layout to space buildings and route paths.</summary>
        public static Dimensions BaseDimensions(AssetCategory kind) => kind switch
        {
            AssetCategory.House => new Dimensions { Width = 6.8f, Depth = 5.4f, WallHeight = 3.6f, RoofRise = 2.3f, Foundation = 0.4f },
            AssetCategory.Shop => new Dimensions { Width = 6.6f, Depth = 5.0f, WallHeight = 3.0f, RoofRise = 2.0f, Foundation = 0.4f },
            _ => new Dimensions { Width = 5.0f, Depth = 4.4f, WallHeight = 2.6f, RoofRise = 2.0f, Foundation = 0.4f },
        };

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            float v = s.ColorVariation;
            Dimensions d = BaseDimensions(kind);
            float w = d.Width * s.Size * Range(rng, 0.96f, 1.04f);
            float depth = d.Depth * s.Size * Range(rng, 0.96f, 1.04f);
            float wallH = d.WallHeight * s.Size;
            float rise = d.RoofRise * s.Size;
            float f = d.Foundation;
            float top = f + wallH;
            float front = depth * 0.5f;

            bool timbered = Timbered(kind, variant);
            VRamp roof = RoofRamp(variant);
            VRamp shutterRamp = roof == VRamp.RoofRed ? VRamp.RoofBlue : VRamp.Tunic;
            int plaster = Mathf.Clamp(Palette.PickShade(rng, 5, v), 4, 7);
            int roofShade = Palette.PickShade(rng, 4, v);
            bool shutters = rng.NextDouble() < 0.6;

            var mb = new MeshBuilder();

            // Foundation and walls.
            Shapes.Box(mb, new Vector3(0f, f * 0.5f, 0f), new Vector3(w + 0.3f, f, depth + 0.3f), GenUtil.Village(VRamp.Stone, 3));
            Shapes.Box(mb, new Vector3(0f, f + wallH * 0.5f, 0f), new Vector3(w, wallH, depth), GenUtil.Village(VRamp.Plaster, plaster));

            if (timbered) TimberFrame(mb, w, depth, f, wallH, kind == AssetCategory.House);

            // Roof and gables.
            float ridgeY = BuildingParts.GableRoof(mb, Vector3.zero, w, depth, top, rise, 0.4f, 0.3f, 0.16f, roof, roofShade);
            BuildingParts.Gables(mb, Vector3.zero, w, depth, top, ridgeY, timbered ? VRamp.Plaster : VRamp.Wood, timbered ? plaster - 1 : 4);

            // Door, step and openings per building type.
            float doorX;
            Quaternion facingFront = Quaternion.identity;
            Quaternion facingBack = Quaternion.LookRotation(Vector3.back);
            Quaternion facingRight = Quaternion.LookRotation(Vector3.right);
            Quaternion facingLeft = Quaternion.LookRotation(Vector3.left);

            switch (kind)
            {
                case AssetCategory.House:
                {
                    doorX = 0f;
                    float lowY = f + 1.25f, highY = f + wallH * 0.5f + 0.9f;
                    foreach (float x in new[] { -(w * 0.5f - 1.15f), w * 0.5f - 1.15f })
                    {
                        BuildingParts.Window(mb, rng, new Vector3(x, lowY, front), facingFront, 0.9f, 0.95f, shutters ? shutterRamp : (VRamp?)null, false);
                        BuildingParts.Window(mb, rng, new Vector3(x, highY, front), facingFront, 0.8f, 0.85f, null, true);
                    }
                    BuildingParts.Window(mb, rng, new Vector3(0f, highY, front), facingFront, 0.7f, 0.8f, null, false);
                    BuildingParts.Window(mb, rng, new Vector3(w * 0.5f, lowY, 0f), facingRight, 0.8f, 0.9f, null, false);
                    BuildingParts.Window(mb, rng, new Vector3(-w * 0.5f, lowY, 0f), facingLeft, 0.8f, 0.9f, null, false);
                    BuildingParts.Window(mb, rng, new Vector3(0f, lowY, -front), facingBack, 0.9f, 0.9f, null, false);
                    break;
                }
                case AssetCategory.Shop:
                {
                    doorX = w * 0.5f - 1.3f;
                    // Display window, striped awning and a hanging sign.
                    BuildingParts.Window(mb, rng, new Vector3(-0.9f, f + 1.25f, front), facingFront, 2.6f, 1.5f, null, false);
                    Awning(mb, w, f + 2.55f, front);
                    Shapes.Box(mb, new Vector3(w * 0.5f - 0.2f, f + 2.95f, front + 0.45f), new Vector3(0.08f, 0.08f, 0.9f), GenUtil.VillageFlat(VRamp.Dark, 2));
                    Shapes.Box(mb, new Vector3(w * 0.5f - 0.2f, f + 2.6f, front + 0.62f), new Vector3(0.06f, 0.5f, 0.75f), GenUtil.Village(VRamp.Wood, 5));
                    Shapes.Box(mb, new Vector3(w * 0.5f - 0.16f, f + 2.6f, front + 0.62f), new Vector3(0.04f, 0.26f, 0.26f), GenUtil.VillageFlat(VRamp.Accent, 4));
                    BuildingParts.Window(mb, rng, new Vector3(w * 0.5f, f + 1.4f, 0f), facingRight, 0.9f, 0.9f, null, false);
                    BuildingParts.Window(mb, rng, new Vector3(-w * 0.5f, f + 1.4f, 0f), facingLeft, 0.9f, 0.9f, null, false);
                    break;
                }
                default: // Cottage
                {
                    doorX = variant % 3 == 2 ? 0.85f : 0f;
                    float y = f + 1.4f;
                    var frontWindows = doorX == 0f ? new[] { -(w * 0.5f - 1.0f), w * 0.5f - 1.0f } : new[] { -1.15f };
                    foreach (float x in frontWindows)
                        BuildingParts.Window(mb, rng, new Vector3(x, y, front), facingFront, 0.85f, 0.85f, shutters ? shutterRamp : (VRamp?)null, true);
                    BuildingParts.Window(mb, rng, new Vector3(w * 0.5f, y, 0f), facingRight, 0.75f, 0.8f, null, false);
                    BuildingParts.Window(mb, rng, new Vector3(-w * 0.5f, y, 0f), facingLeft, 0.75f, 0.8f, null, false);
                    BuildingParts.Window(mb, rng, new Vector3(0f, y, -front), facingBack, 0.8f, 0.8f, null, false);
                    // Little canopy over the door.
                    Shapes.Box(mb, new Vector3(doorX, f + 2.25f, front + 0.32f), new Vector3(1.6f, 0.08f, 0.7f), Quaternion.Euler(20f, 0f, 0f),
                        n => VillagePalette.UV(roof, roofShade + (n.y > 0.3f ? 0 : -2)));
                    break;
                }
            }

            BuildingParts.Door(mb, doorX, f, front, kind == AssetCategory.House ? 1.1f : 1.0f, 1.9f);
            Shapes.Box(mb, new Vector3(doorX, f * 0.3f, front + 0.45f), new Vector3(1.5f, f * 0.6f, 0.6f), GenUtil.Village(VRamp.Stone, 4));

            // Chimney (houses always; cottages and shops by seed).
            if (kind == AssetCategory.House || rng.NextDouble() < 0.7)
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float baseY = top - 0.3f, chimneyTop = ridgeY + 0.5f;
                var c = new Vector3(side * (w * 0.5f - 0.8f), (baseY + chimneyTop) * 0.5f, -depth * 0.22f);
                Shapes.Box(mb, c, new Vector3(0.6f, chimneyTop - baseY, 0.6f), GenUtil.Village(VRamp.Stone, 3));
                Shapes.Box(mb, new Vector3(c.x, chimneyTop + 0.06f, c.z), new Vector3(0.74f, 0.12f, 0.74f), GenUtil.Village(VRamp.Stone, 2));
            }

            return new GeneratedMesh
            {
                Mesh = mb.ToMesh(meshName),
                Collider = ColliderKind.Boxes,
                // One box for walls and foundation: cheap, and the roof is out of jumping reach.
                Boxes = new[] { GenUtil.Box(new Vector3(0f, top * 0.5f, 0f), new Vector3(w + 0.3f, top, depth + 0.3f)) },
            };
        }

        static void TimberFrame(MeshBuilder mb, float w, float depth, float f, float wallH, bool midBand)
        {
            var dark = GenUtil.VillageFlat(VRamp.TimberDark, 3);
            foreach (float x in new[] { -w * 0.5f, w * 0.5f })
                foreach (float z in new[] { -depth * 0.5f, depth * 0.5f })
                    Shapes.Box(mb, new Vector3(x, f + wallH * 0.5f, z), new Vector3(0.18f, wallH, 0.18f), dark);

            var bands = new List<float> { f + wallH - 0.08f, f + 0.08f };
            if (midBand) bands.Add(f + wallH * 0.5f);
            foreach (float y in bands)
            {
                Shapes.Box(mb, new Vector3(0f, y, depth * 0.5f + 0.02f), new Vector3(w + 0.18f, 0.16f, 0.12f), dark);
                Shapes.Box(mb, new Vector3(0f, y, -depth * 0.5f - 0.02f), new Vector3(w + 0.18f, 0.16f, 0.12f), dark);
                Shapes.Box(mb, new Vector3(w * 0.5f + 0.02f, y, 0f), new Vector3(0.12f, 0.16f, depth + 0.18f), dark);
                Shapes.Box(mb, new Vector3(-w * 0.5f - 0.02f, y, 0f), new Vector3(0.12f, 0.16f, depth + 0.18f), dark);
            }
        }

        static void Awning(MeshBuilder mb, float w, float y, float front)
        {
            float span = w - 0.6f;
            int stripes = Mathf.Max(4, Mathf.RoundToInt(span / 0.45f));
            float stripeW = span / stripes;
            Quaternion tilt = Quaternion.Euler(18f, 0f, 0f);
            for (int i = 0; i < stripes; i++)
            {
                float x = -span * 0.5f + stripeW * (i + 0.5f);
                VRamp ramp = i % 2 == 0 ? VRamp.Accent : VRamp.Plaster;
                Shapes.Box(mb, new Vector3(x, y - 0.17f, front + 0.55f), new Vector3(stripeW, 0.06f, 1.15f), tilt,
                    n => VillagePalette.UV(ramp, (ramp == VRamp.Accent ? 4 : 6) + (n.y > 0.3f ? 0 : -2)));
            }
        }
    }
}
