using System;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Wildlife;
using UnityEngine;

namespace Greenhaven.Editor.Wildlife
{
    /// <summary>
    /// The five original animals, authored as lofted, faceted parts on the wildlife palette and skinned
    /// rigidly to a small skeleton (body, neck, head, ears, tail, legs, wings). Feet at y = 0, facing +Z.
    /// Deterministic: (settings, species, variant) always produce the same mesh.
    /// </summary>
    public static class AnimalModels
    {
        public const string Body = "Body", Neck = "Neck", Head = "Head", EarL = "Ear.L", EarR = "Ear.R", Tail0 = "Tail.0", Tail1 = "Tail.1";
        public const string LegFL = "Leg.FL", LegFR = "Leg.FR", LegHL = "Leg.HL", LegHR = "Leg.HR", WingL = "Wing.L", WingR = "Wing.R", LegL = "Leg.L", LegR = "Leg.R";

        public static string PrefabName(AnimalKind kind) => kind switch
        {
            AnimalKind.Fox => "GreenhavenFox",
            AnimalKind.Deer => "ForestDeer",
            AnimalKind.Rabbit => "ForestRabbit",
            AnimalKind.Goat => "CloudpeakGoat",
            _ => "ForestBird",
        };

        public static string StyleName(AnimalKind kind, int variant) => kind switch
        {
            AnimalKind.Deer => variant == 0 ? "Stag" : "Doe",
            AnimalKind.Bird => new[] { "Bluebird", "Robin", "Finch" }[variant % 3],
            AnimalKind.Goat => variant % 2 == 0 ? "White" : "Grey",
            _ => variant == 0 ? "Classic" : $"Variant {variant}",
        };

        public static AnimalBuilder Build(AnimalKind kind, int variant, WildlifeGeneratorSettings s, Palette palette)
        {
            var rng = new System.Random(unchecked(s.seed * 73856093 ^ ((int)kind + 1) * 19349663 ^ (variant + 1) * 83492791));
            float size = 1f + ((float)rng.NextDouble() * 2f - 1f) * s.sizeVariation;
            int shade = 4 + (rng.NextDouble() < s.colorVariation * 0.7 ? (rng.Next(2) * 2 - 1) : 0);
            Vector2 P(WRamp ramp, int sh) => palette.UV((int)ramp, Mathf.Clamp(sh, 0, Palette.Shades - 1));
            var b = new AnimalBuilder();
            switch (kind)
            {
                case AnimalKind.Fox: Fox(b, s, rng, P, shade, size * s.foxScale); break;
                case AnimalKind.Deer: Deer(b, s, rng, P, shade, size * s.deerScale * (variant == 0 ? 1.05f : 0.95f), variant == 0); break;
                case AnimalKind.Rabbit: Rabbit(b, s, rng, P, shade + (variant % 2), size * s.rabbitScale); break;
                case AnimalKind.Goat: Goat(b, s, rng, P, variant % 2 == 0 ? shade + 1 : shade - 1, size * s.goatScale); break;
                default: Bird(b, rng, P, variant % 3, size * s.birdScale); break;
            }
            return b;
        }

        static readonly float[] Sides = { -1f, 1f };

        static void Eyes(AnimalBuilder b, int head, Func<WRamp, int, Vector2> P, float x, float y, float z, float size, float yaw)
        {
            foreach (float sx in Sides)
            {
                b.Box(head, new Vector3(sx * x, y, z), new Vector3(size * 0.8f, size, size * 0.55f), Quaternion.Euler(0f, sx * yaw, 0f), _ => P(WRamp.Dark, 1));
                b.Box(head, new Vector3(sx * (x + size * 0.15f), y + size * 0.25f, z + size * 0.3f), Vector3.one * size * 0.32f, _ => P(WRamp.Eye, 7));
            }
        }

        // ───────────────────────────── Fox (quality benchmark) ─────────────────────────────

        static void Fox(AnimalBuilder b, WildlifeGeneratorSettings s, System.Random rng, Func<WRamp, int, Vector2> P, int shade, float scale)
        {
            b.Scale = scale;
            int body = b.Bone(Body, -1, new Vector3(0f, 0.40f, 0f));
            int neck = b.Bone(Neck, body, new Vector3(0f, 0.46f, 0.19f));
            int head = b.Bone(Head, neck, new Vector3(0f, 0.555f, 0.28f));
            int earL = b.Bone(EarL, head, new Vector3(-0.045f, 0.62f, 0.30f));
            int earR = b.Bone(EarR, head, new Vector3(0.045f, 0.62f, 0.30f));
            int tail0 = b.Bone(Tail0, body, new Vector3(0f, 0.44f, -0.25f));
            int tail1 = b.Bone(Tail1, tail0, new Vector3(0f, 0.37f, -0.48f));
            int[] legs =
            {
                b.Bone(LegFL, body, new Vector3(-0.075f, 0.36f, 0.17f)), b.Bone(LegFR, body, new Vector3(0.075f, 0.36f, 0.17f)),
                b.Bone(LegHL, body, new Vector3(-0.08f, 0.37f, -0.16f)), b.Bone(LegHR, body, new Vector3(0.08f, 0.37f, -0.16f)),
            };

            Vector2 Coat(Vector3 n) => P(WRamp.FoxCoat, shade + (n.y > 0.6f ? 1 : 0));
            Vector2 Cream() => P(WRamp.Cream, 5);

            // Torso: orange back, cream belly and chest.
            b.Tube(body,
                new[] { V(0, .41f, -.27f), V(0, .42f, -.17f), V(0, .425f, 0), V(0, .43f, .14f), V(0, .45f, .23f) },
                new[] { R(.08f, .085f), R(.108f, .113f), R(.113f, .118f), R(.102f, .112f), R(.075f, .086f) }, 8,
                (n, c) => n.y < -0.45f || (n.z > 0.55f && c.y < 0.44f) ? Cream() : Coat(n));
            // Neck with a cream bib.
            b.Tube(neck, new[] { V(0, .43f, .16f), V(0, .50f, .24f), V(0, .56f, .29f) }, new[] { R(.065f, .07f), R(.058f, .06f), R(.052f, .052f) }, 7,
                (n, c) => n.z > 0.15f && n.y < 0.35f ? Cream() : Coat(n));
            // Head: rounded skull with white cheeks, then a tapering muzzle.
            b.Tube(head, new[] { V(0, .565f, .24f), V(0, .585f, .29f), V(0, .585f, .345f) }, new[] { R(.06f, .055f), R(.082f, .072f), R(.068f, .06f) }, 8,
                (n, c) => n.y < -0.25f ? Cream() : Coat(n));
            b.Tube(head, new[] { V(0, .575f, .34f), V(0, .565f, .39f), V(0, .555f, .44f) }, new[] { R(.048f, .04f), R(.034f, .029f), R(.014f, .012f) }, 7,
                (n, c) => n.y < 0.25f ? Cream() : Coat(n), capStart: false);
            b.Box(head, V(0, .556f, .447f), V(.022f, .018f, .018f), _ => P(WRamp.Dark, 1));
            Eyes(b, head, P, 0.04f, 0.597f, 0.333f, 0.022f, 25f);
            // Pointed ears: orange outside, cream inside, dark tips.
            foreach ((int bone, float sx) in new[] { (earL, -1f), (earR, 1f) })
            {
                b.Cone(bone, V(sx * .045f, .61f, .30f), V(sx * .064f, .715f, .285f), .038f, .014f, 3, n => n.z > 0.3f ? P(WRamp.Cream, 6) : Coat(n));
                b.Cone(bone, V(sx * .064f, .715f, .285f), V(sx * .068f, .755f, .28f), .014f, .003f, 3, _ => P(WRamp.Dark, 2));
            }
            // Legs with dark "socks" and paws.
            for (int i = 0; i < 4; i++)
            {
                bool hind = i >= 2;
                float x = (i % 2 == 0 ? -1f : 1f) * (hind ? 0.08f : 0.075f), z = hind ? -0.16f : 0.17f;
                Vector3[] centres = hind
                    ? new[] { V(x, .41f, z), V(x, .30f, z - .01f), V(x, .18f, z - .025f), V(x, .05f, z - .01f) }
                    : new[] { V(x, .40f, z), V(x, .25f, z), V(x, .12f, z + .005f), V(x, .04f, z + .01f) };
                Vector2[] radii = hind
                    ? new[] { R(.062f, .075f), R(.048f, .056f), R(.034f, .036f), R(.031f, .033f) }
                    : new[] { R(.05f, .056f), R(.039f, .042f), R(.032f, .033f), R(.03f, .032f) };
                b.Tube(legs[i], centres, radii, 6, (n, c) => c.y < 0.13f ? P(WRamp.Dark, 2) : Coat(n), capStart: false);
                b.Box(legs[i], V(x, .017f, z + .02f), V(.052f, .034f, .07f), _ => P(WRamp.Dark, 1));
            }
            // Big bushy tail with a cream tip, in two bones so it can curl.
            float ts = s.foxTail;
            Vector3 t0 = V(0, .43f, -.24f);
            Vector3 T(float y, float z) => t0 + (V(0, y, z) - t0) * ts;
            Vector3[] tail = { t0, T(.42f, -.33f), T(.39f, -.44f), T(.35f, -.54f), T(.31f, -.63f), T(.29f, -.70f), T(.285f, -.73f) };
            float[] tr = { .035f, .07f, .095f, .10f, .085f, .055f, .016f };
            Vector2 TR(int i) => R(tr[i] * ts * 1.05f, tr[i] * ts);
            b.Tube(tail0, new[] { tail[0], tail[1], tail[2], tail[3] }, new[] { TR(0), TR(1), TR(2), TR(3) }, 8,
                (n, c) => P(WRamp.FoxCoat, shade + (n.y < -0.5f ? 1 : 0)), capStart: false, capEnd: false);
            b.Tube(tail1, new[] { tail[3], tail[4], tail[5], tail[6] }, new[] { TR(3), TR(4), TR(5), TR(6) }, 8,
                (n, c) => c.z < t0.z + (-.60f - t0.z) * ts ? P(WRamp.Cream, 6) : P(WRamp.FoxCoat, shade + (n.y < -0.5f ? 1 : 0)), capStart: false);
        }

        // ───────────────────────────── Deer ─────────────────────────────

        static void Deer(AnimalBuilder b, WildlifeGeneratorSettings s, System.Random rng, Func<WRamp, int, Vector2> P, int shade, float scale, bool antlers)
        {
            b.Scale = scale;
            const float legTop = 0.84f;
            float extra = legTop * (s.deerLegs - 1f);
            b.Warp = p => new Vector3(p.x, p.y <= legTop ? p.y * s.deerLegs : p.y + extra, p.z);

            int body = b.Bone(Body, -1, V(0, .94f, 0));
            int neck = b.Bone(Neck, body, V(0, 1.0f, .36f));
            int head = b.Bone(Head, neck, V(0, 1.40f, .56f));
            int earL = b.Bone(EarL, head, V(-.06f, 1.47f, .55f));
            int earR = b.Bone(EarR, head, V(.06f, 1.47f, .55f));
            int tail0 = b.Bone(Tail0, body, V(0, 1.0f, -.43f));
            int[] legs =
            {
                b.Bone(LegFL, body, V(-.10f, .86f, .30f)), b.Bone(LegFR, body, V(.10f, .86f, .30f)),
                b.Bone(LegHL, body, V(-.11f, .88f, -.30f)), b.Bone(LegHR, body, V(.11f, .88f, -.30f)),
            };
            Vector2 Coat(Vector3 n) => P(WRamp.DeerCoat, shade + (n.y > 0.6f ? 1 : 0));
            Vector2 Light() => P(WRamp.Cream, 5);

            b.Tube(body,
                new[] { V(0, .95f, -.45f), V(0, .97f, -.28f), V(0, .975f, -.02f), V(0, .99f, .22f), V(0, 1.03f, .38f) },
                new[] { R(.14f, .16f), R(.175f, .205f), R(.18f, .215f), R(.165f, .205f), R(.12f, .16f) }, 8,
                (n, c) => n.y < -0.45f ? Light() : n.z < -0.65f ? P(WRamp.Cream, 6) : Coat(n));
            b.Tube(neck, new[] { V(0, .98f, .32f), V(0, 1.10f, .40f), V(0, 1.24f, .47f), V(0, 1.38f, .53f) },
                new[] { R(.10f, .125f), R(.08f, .092f), R(.066f, .07f), R(.058f, .06f) }, 7,
                (n, c) => n.z > 0.2f && n.y < 0.3f ? Light() : Coat(n));
            b.Tube(head, new[] { V(0, 1.41f, .50f), V(0, 1.43f, .56f), V(0, 1.42f, .62f) }, new[] { R(.06f, .06f), R(.072f, .07f), R(.058f, .056f) }, 8,
                (n, c) => n.y < -0.3f ? Light() : Coat(n));
            b.Tube(head, new[] { V(0, 1.41f, .61f), V(0, 1.38f, .68f), V(0, 1.355f, .745f) }, new[] { R(.05f, .048f), R(.036f, .036f), R(.02f, .02f) }, 7,
                (n, c) => n.y < 0.1f ? Light() : P(WRamp.DeerCoat, shade - 1), capStart: false);
            b.Box(head, V(0, 1.355f, .752f), V(.03f, .025f, .02f), _ => P(WRamp.Dark, 1));
            Eyes(b, head, P, 0.052f, 1.445f, 0.585f, 0.026f, 25f);
            foreach ((int bone, float sx) in new[] { (earL, -1f), (earR, 1f) })
                b.Cone(bone, V(sx * .05f, 1.46f, .55f), V(sx * .15f, 1.53f, .53f), .04f, .012f, 4, n => n.z > 0.3f ? P(WRamp.Cream, 6) : Coat(n));
            if (antlers)
            {
                Vector2 Antler(Vector3 n) => P(WRamp.Antler, 5 + (n.y > 0.5f ? 1 : 0));
                foreach (float sx in Sides)
                {
                    b.Cone(head, V(sx * .035f, 1.47f, .54f), V(sx * .12f, 1.66f, .47f), .019f, .011f, 5, Antler);
                    b.Cone(head, V(sx * .07f, 1.55f, .51f), V(sx * .08f, 1.65f, .58f), .011f, .004f, 4, Antler);
                    b.Cone(head, V(sx * .12f, 1.66f, .47f), V(sx * .18f, 1.74f, .43f), .011f, .004f, 4, Antler);
                    b.Cone(head, V(sx * .105f, 1.63f, .48f), V(sx * .06f, 1.73f, .50f), .009f, .003f, 4, Antler);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                bool hind = i >= 2;
                float x = (i % 2 == 0 ? -1f : 1f) * (hind ? 0.11f : 0.10f), z = hind ? -0.30f : 0.30f;
                Vector3[] centres = hind
                    ? new[] { V(x, .97f, z), V(x, .70f, z - .04f), V(x, .45f, z - .06f), V(x, .22f, z - .02f), V(x, .05f, z) }
                    : new[] { V(x, .95f, z), V(x, .62f, z), V(x, .40f, z + .01f), V(x, .22f, z), V(x, .05f, z + .01f) };
                Vector2[] radii = hind
                    ? new[] { R(.095f, .115f), R(.068f, .08f), R(.042f, .046f), R(.036f, .038f), R(.033f, .035f) }
                    : new[] { R(.08f, .09f), R(.055f, .06f), R(.041f, .043f), R(.036f, .038f), R(.033f, .035f) };
                b.Tube(legs[i], centres, radii, 6, (n, c) => c.y < 0.35f ? P(WRamp.DeerCoat, shade - 1) : Coat(n), capStart: false);
                b.Box(legs[i], V(x, .025f, z + .01f), V(.06f, .05f, .07f), _ => P(WRamp.Dark, 1));
            }
            b.Tube(tail0, new[] { V(0, 1.0f, -.44f), V(0, .97f, -.50f), V(0, .92f, -.54f) }, new[] { R(.035f, .035f), R(.045f, .035f), R(.02f, .016f) }, 6,
                (n, c) => n.y < 0f || n.z < -0.3f ? P(WRamp.Cream, 7) : Coat(n), capStart: false);
        }

        // ───────────────────────────── Rabbit ─────────────────────────────

        static void Rabbit(AnimalBuilder b, WildlifeGeneratorSettings s, System.Random rng, Func<WRamp, int, Vector2> P, int shade, float scale)
        {
            b.Scale = scale;
            int body = b.Bone(Body, -1, V(0, .13f, 0));
            int head = b.Bone(Head, body, V(0, .20f, .11f));
            int earL = b.Bone(EarL, head, V(-.022f, .245f, .115f));
            int earR = b.Bone(EarR, head, V(.022f, .245f, .115f));
            int tail0 = b.Bone(Tail0, body, V(0, .15f, -.12f));
            int[] legs =
            {
                b.Bone(LegFL, body, V(-.035f, .09f, .07f)), b.Bone(LegFR, body, V(.035f, .09f, .07f)),
                b.Bone(LegHL, body, V(-.05f, .10f, -.05f)), b.Bone(LegHR, body, V(.05f, .10f, -.05f)),
            };
            Vector2 Coat(Vector3 n) => P(WRamp.RabbitCoat, shade + (n.y > 0.6f ? 1 : 0));
            Vector2 Cream(int sh = 5) => P(WRamp.Cream, sh);

            b.Tube(body, new[] { V(0, .12f, -.12f), V(0, .135f, -.06f), V(0, .145f, .02f), V(0, .155f, .08f) },
                new[] { R(.06f, .065f), R(.085f, .088f), R(.078f, .083f), R(.055f, .062f) }, 8, (n, c) => n.y < -0.45f ? Cream() : Coat(n));
            b.Tube(head, new[] { V(0, .195f, .085f), V(0, .21f, .125f), V(0, .205f, .165f), V(0, .192f, .19f) },
                new[] { R(.045f, .045f), R(.055f, .052f), R(.04f, .037f), R(.016f, .014f) }, 8, (n, c) => n.y < -0.2f ? Cream() : Coat(n));
            b.Box(head, V(0, .195f, .196f), V(.014f, .01f, .008f), _ => P(WRamp.Dark, 4));
            Eyes(b, head, P, 0.038f, 0.222f, 0.148f, 0.02f, 30f);
            float re = s.rabbitEars;
            foreach ((int bone, float sx) in new[] { (earL, -1f), (earR, 1f) })
                b.Tube(bone, new[] { V(sx * .022f, .24f, .115f), V(sx * .03f, .24f + .075f * re, .105f), V(sx * .034f, .24f + .15f * re, .095f) },
                    new[] { R(.015f, .008f), R(.021f, .009f), R(.012f, .006f) }, 6, (n, c) => n.z > 0.3f ? Cream(6) : Coat(n), capStart: false);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.035f : 0.035f;
                b.Tube(legs[i], new[] { V(x, .11f, .07f), V(x, .05f, .075f), V(x, .015f, .085f) }, new[] { R(.02f, .022f), R(.016f, .017f), R(.015f, .016f) }, 6,
                    (n, c) => Coat(n), capStart: false);
                b.Box(legs[i], V(x, .008f, .092f), V(.026f, .016f, .03f), _ => Cream());
            }
            for (int i = 2; i < 4; i++)
            {
                float sx = i == 2 ? -1f : 1f;
                b.Blob(legs[i], rng, V(sx * .055f, .105f, -.055f), .048f, new Vector3(0.6f, 1f, 1.15f), Coat);
                b.Box(legs[i], V(sx * .045f, .013f, -.035f), V(.03f, .024f, .085f), _ => P(WRamp.RabbitCoat, shade + 1));
            }
            b.Blob(tail0, rng, V(0, .15f, -.135f), .032f, new Vector3(1f, 1f, 0.9f), _ => Cream(7));
        }

        // ───────────────────────────── Goat ─────────────────────────────

        static void Goat(AnimalBuilder b, WildlifeGeneratorSettings s, System.Random rng, Func<WRamp, int, Vector2> P, int shade, float scale)
        {
            b.Scale = scale;
            int body = b.Bone(Body, -1, V(0, .64f, 0));
            int neck = b.Bone(Neck, body, V(0, .70f, .28f));
            int head = b.Bone(Head, neck, V(0, .94f, .42f));
            int earL = b.Bone(EarL, head, V(-.05f, .97f, .42f));
            int earR = b.Bone(EarR, head, V(.05f, .97f, .42f));
            int tail0 = b.Bone(Tail0, body, V(0, .70f, -.33f));
            int[] legs =
            {
                b.Bone(LegFL, body, V(-.09f, .57f, .24f)), b.Bone(LegFR, body, V(.09f, .57f, .24f)),
                b.Bone(LegHL, body, V(-.095f, .59f, -.24f)), b.Bone(LegHR, body, V(.095f, .59f, -.24f)),
            };
            Vector2 Coat(Vector3 n) => P(WRamp.GoatCoat, shade + (n.y > 0.6f ? 1 : n.y < -0.5f ? -1 : 0));

            b.Tube(body, new[] { V(0, .63f, -.34f), V(0, .65f, -.17f), V(0, .655f, .05f), V(0, .67f, .22f), V(0, .70f, .32f) },
                new[] { R(.13f, .15f), R(.165f, .18f), R(.17f, .185f), R(.155f, .17f), R(.11f, .13f) }, 8, (n, c) => Coat(n));
            b.Tube(neck, new[] { V(0, .68f, .26f), V(0, .78f, .33f), V(0, .88f, .39f), V(0, .95f, .42f) },
                new[] { R(.085f, .10f), R(.07f, .08f), R(.06f, .065f), R(.055f, .058f) }, 7, (n, c) => Coat(n));
            b.Tube(head, new[] { V(0, .96f, .38f), V(0, .975f, .44f), V(0, .965f, .49f) }, new[] { R(.058f, .06f), R(.068f, .066f), R(.055f, .053f) }, 8, (n, c) => Coat(n));
            b.Tube(head, new[] { V(0, .955f, .48f), V(0, .925f, .535f), V(0, .90f, .58f) }, new[] { R(.045f, .045f), R(.034f, .034f), R(.02f, .02f) }, 7,
                (n, c) => Coat(n), capStart: false);
            b.Box(head, V(0, .90f, .587f), V(.026f, .02f, .014f), _ => P(WRamp.Dark, 1));
            b.Cone(head, V(0, .89f, .535f), V(0, .80f, .525f), .022f, .006f, 4, _ => P(WRamp.GoatCoat, shade - 2));
            foreach (float sx in Sides)
            {
                b.Box(head, V(sx * .052f, .99f, .465f), V(.02f, .022f, .012f), Quaternion.Euler(0f, sx * 25f, 0f), _ => P(WRamp.Beak, 5));
                b.Box(head, V(sx * .055f, .99f, .471f), V(.012f, .006f, .006f), Quaternion.Euler(0f, sx * 25f, 0f), _ => P(WRamp.Dark, 1));
            }
            foreach ((int bone, float sx) in new[] { (earL, -1f), (earR, 1f) })
                b.Cone(bone, V(sx * .05f, .965f, .42f), V(sx * .16f, .935f, .40f), .027f, .01f, 4, n => n.z > 0.3f ? P(WRamp.Cream, 5) : Coat(n));
            // Curved horns sweeping back.
            float gh = s.goatHorns;
            foreach (float sx in Sides)
            {
                Vector3 h0 = V(sx * .03f, 1.0f, .43f);
                Vector3 H(float x, float y, float z) => h0 + (V(sx * x, y, z) - h0) * gh;
                b.Tube(head, new[] { h0, H(.05f, 1.075f, .39f), H(.068f, 1.115f, .32f), H(.085f, 1.10f, .245f), H(.095f, 1.045f, .205f) },
                    new[] { R(.026f, .03f), R(.022f, .025f), R(.017f, .019f), R(.012f, .013f), R(.006f, .006f) }, 6,
                    (n, c) => P(WRamp.Horn, 4 + (n.y > 0.4f ? 1 : 0)), capStart: false);
            }
            for (int i = 0; i < 4; i++)
            {
                bool hind = i >= 2;
                float x = (i % 2 == 0 ? -1f : 1f) * (hind ? 0.095f : 0.09f), z = hind ? -0.24f : 0.24f;
                b.Tube(legs[i], new[] { V(x, .66f, z), V(x, .40f, z), V(x, .22f, z + .005f), V(x, .05f, z + .01f) },
                    new[] { R(.06f, .07f), R(.04f, .044f), R(.033f, .035f), R(.031f, .033f) }, 6,
                    (n, c) => c.y < 0.35f ? P(WRamp.GoatCoat, shade - 2) : Coat(n), capStart: false);
                b.Box(legs[i], V(x, .025f, z + .01f), V(.05f, .05f, .06f), _ => P(WRamp.Dark, 1));
            }
            b.Cone(tail0, V(0, .70f, -.33f), V(0, .77f, -.40f), .028f, .01f, 5, Coat);
        }

        // ───────────────────────────── Bird ─────────────────────────────

        static void Bird(AnimalBuilder b, System.Random rng, Func<WRamp, int, Vector2> P, int style, float scale)
        {
            b.Scale = scale;
            WRamp bodyRamp = style == 0 ? WRamp.BirdBlue : style == 1 ? WRamp.RobinBrown : WRamp.FinchYellow;
            WRamp breastRamp = style == 0 ? WRamp.BirdBreast : style == 1 ? WRamp.FoxCoat : WRamp.Cream;
            Vector2 BodyC(int d = 0) => P(bodyRamp, 4 + d);
            Vector2 Wing() => style == 2 ? P(WRamp.Dark, 3) : P(bodyRamp, 3);

            int body = b.Bone(Body, -1, V(0, .10f, 0));
            int head = b.Bone(Head, body, V(0, .135f, .045f));
            int tail0 = b.Bone(Tail0, body, V(0, .095f, -.045f));
            int wingL = b.Bone(WingL, body, V(-.032f, .118f, .02f));
            int wingR = b.Bone(WingR, body, V(.032f, .118f, .02f));
            int legL = b.Bone(LegL, body, V(-.012f, .07f, 0f));
            int legR = b.Bone(LegR, body, V(.012f, .07f, 0f));

            b.Tube(body, new[] { V(0, .088f, -.05f), V(0, .10f, -.02f), V(0, .11f, .015f), V(0, .123f, .04f) },
                new[] { R(.024f, .024f), R(.04f, .042f), R(.038f, .04f), R(.026f, .027f) }, 8,
                (n, c) => n.y < -0.5f ? P(WRamp.Cream, 6) : n.z > 0.15f && n.y < 0.35f ? P(breastRamp, 4) : BodyC(n.y > 0.6f ? 1 : 0));
            b.Blob(head, rng, V(0, .142f, .055f), .029f, new Vector3(1f, 1f, 1.08f), n => n.y < -0.3f ? P(breastRamp, 4) : BodyC(1), 0.03f);
            b.Cone(head, V(0, .139f, .078f), V(0, .135f, .104f), .009f, .002f, 4, _ => P(WRamp.Beak, 5));
            foreach (float sx in Sides)
            {
                b.Box(head, V(sx * .021f, .149f, .067f), V(.009f, .011f, .006f), Quaternion.Euler(0f, sx * 30f, 0f), _ => P(WRamp.Dark, 1));
                b.Box(head, V(sx * .023f, .152f, .07f), Vector3.one * .004f, _ => P(WRamp.Eye, 7));
            }
            b.Blob(wingL, rng, V(-.04f, .112f, -.012f), .048f, new Vector3(.22f, .32f, 1f), _ => Wing(), 0.02f);
            b.Blob(wingR, rng, V(.04f, .112f, -.012f), .048f, new Vector3(.22f, .32f, 1f), _ => Wing(), 0.02f);
            b.Box(tail0, V(0, .093f, -.085f), V(.036f, .008f, .065f), Quaternion.Euler(-12f, 0f, 0f), _ => Wing());
            foreach ((int bone, float sx) in new[] { (legL, -1f), (legR, 1f) })
            {
                b.Cone(bone, V(sx * .012f, .072f, 0f), V(sx * .012f, .006f, .004f), .0045f, .003f, 4, _ => P(WRamp.Beak, 2));
                b.Box(bone, V(sx * .012f, .003f, .01f), V(.008f, .004f, .022f), _ => P(WRamp.Beak, 2));
            }
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector2 R(float side, float other) => new Vector2(side, other);
    }
}
