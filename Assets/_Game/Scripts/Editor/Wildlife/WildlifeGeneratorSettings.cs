using Greenhaven.Editor.AssetGenerator;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.Wildlife
{
    /// <summary>Colour rows of the wildlife palette (one dark→light ramp each).</summary>
    public enum WRamp { FoxCoat, Cream, Dark, DeerCoat, Antler, RabbitCoat, GoatCoat, Horn, BirdBlue, BirdBreast, Beak, Eye, RobinBrown, FinchYellow }

    /// <summary>
    /// Colours, proportions and variation for the generated animals. Lives at
    /// Assets/_Game/Generated/Wildlife/WildlifeGeneratorSettings.asset; edit it in the Inspector and run
    /// Greenhaven ▸ Wildlife ▸ Generate Wildlife again. Same settings → identical animals.
    /// </summary>
    public sealed class WildlifeGeneratorSettings : ScriptableObject
    {
        public const string AssetPath = WildlifeGenerator.Root + "/WildlifeGeneratorSettings.asset";

        [Tooltip("Same seed and settings always give the same animals.")] public int seed = 1234;
        [Range(0f, 0.3f), Tooltip("Size difference between variants.")] public float sizeVariation = 0.08f;
        [Range(0f, 1f), Tooltip("Shade difference between variants.")] public float colorVariation = 0.4f;

        [Header("Colours")]
        public Color foxCoat = Palette.Hex(0xE0662A);
        public Color cream = Palette.Hex(0xF6EBD3);
        public Color dark = Palette.Hex(0x2E2420);
        public Color deerCoat = Palette.Hex(0xBE8A55);
        public Color antler = Palette.Hex(0xE2CFAE);
        public Color rabbitCoat = Palette.Hex(0xA88C70);
        public Color goatCoat = Palette.Hex(0xEDE7DA);
        public Color horn = Palette.Hex(0x7A6550);
        public Color birdBlue = Palette.Hex(0x4C86CC);
        public Color birdBreast = Palette.Hex(0xF0A33C);
        public Color beak = Palette.Hex(0xF2B640);
        public Color eyeShine = Color.white;
        public Color robinBrown = Palette.Hex(0x8A6A4E);
        public Color finchYellow = Palette.Hex(0xF4CF3A);

        [Header("Proportions")]
        [Range(0.6f, 1.6f)] public float foxScale = 1.15f;
        [Range(0.6f, 1.5f)] public float foxTail = 1f;
        [Range(0.6f, 1.4f)] public float deerScale = 1f;
        [Range(0.8f, 1.25f)] public float deerLegs = 1f;
        [Range(0.6f, 1.8f)] public float rabbitScale = 1.3f;
        [Range(0.6f, 1.5f)] public float rabbitEars = 1f;
        [Range(0.6f, 1.4f)] public float goatScale = 1f;
        [Range(0.5f, 1.6f)] public float goatHorns = 1f;
        [Range(0.6f, 2f)] public float birdScale = 1.3f;

        [Header("Variants per species")]
        [Range(1, 4)] public int foxVariants = 2;
        [Range(1, 4), Tooltip("Variant 0 is a stag with antlers; the others are does.")] public int deerVariants = 2;
        [Range(1, 4)] public int rabbitVariants = 2;
        [Range(1, 4)] public int goatVariants = 2;
        [Range(1, 3), Tooltip("Bluebird, robin, finch.")] public int birdVariants = 3;

        public Palette ToPalette() => new Palette("WildlifePalette",
            Ramp(foxCoat), Ramp(cream), Ramp(dark), Ramp(deerCoat), Ramp(antler), Ramp(rabbitCoat), Ramp(goatCoat), Ramp(horn),
            Ramp(birdBlue), Ramp(birdBreast), Ramp(beak), Ramp(eyeShine), Ramp(robinBrown), Ramp(finchYellow));

        /// <summary>Ramp whose shade 4 (of 0..7) is exactly the chosen colour: darker below, lighter above.</summary>
        static (Color, Color) Ramp(Color c)
        {
            Color darkEnd = c * 0.55f;
            Color lightEnd = c + (c - darkEnd) * 0.75f;
            lightEnd = new Color(Mathf.Clamp01(lightEnd.r), Mathf.Clamp01(lightEnd.g), Mathf.Clamp01(lightEnd.b));
            darkEnd.a = lightEnd.a = 1f;
            return (darkEnd, lightEnd);
        }

        public int VariantCount(Greenhaven.Wildlife.AnimalKind kind) => kind switch
        {
            Greenhaven.Wildlife.AnimalKind.Fox => foxVariants,
            Greenhaven.Wildlife.AnimalKind.Deer => deerVariants,
            Greenhaven.Wildlife.AnimalKind.Rabbit => rabbitVariants,
            Greenhaven.Wildlife.AnimalKind.Goat => goatVariants,
            _ => birdVariants,
        };

        /// <summary>The project's settings asset, created with defaults if missing (pass false for an unsaved instance).</summary>
        public static WildlifeGeneratorSettings LoadOrCreate(bool createAsset = true)
        {
            var settings = AssetDatabase.LoadAssetAtPath<WildlifeGeneratorSettings>(AssetPath);
            if (settings != null) return settings;
            settings = CreateInstance<WildlifeGeneratorSettings>();
            if (!createAsset) return settings;
            WildlifeGenerator.EnsureFolders();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SetLabels(settings, new[] { GeneratedAssetStore.Label });
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
