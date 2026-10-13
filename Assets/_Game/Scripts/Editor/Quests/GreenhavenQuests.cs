using Greenhaven.Collectibles;
using Greenhaven.Quests;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.Quests
{
    /// <summary>
    /// The MVP's quest definitions, shared by every scene builder so ids, steps and texts stay consistent.
    /// The three missions climb the mountain in order: the forest trail, the Eagle Ledge, the summit.
    /// </summary>
    public static class GreenhavenQuests
    {
        public struct Set
        {
            public QuestDefinition Supplies, Request, MountainClue, Feathers;
            public QuestDefinition[] All => new[] { Supplies, Request, MountainClue, Feathers };
        }

        public const string ShrineLandmark = "summit_shrine", CairnLandmark = "cairn_puzzle";

        public static Set Ensure()
        {
            Phase1SceneBuilder.EnsureFolder(Phase1SceneBuilder.QuestFolder);
            var set = new Set();
            set.Supplies = Phase1SceneBuilder.QuestAsset("Quest_MissingSupplies", q => q.Configure("missing_supplies", "The Missing Supplies",
                "Mira's supply crates fell off her cart along the trail north of the village.", null,
                new QuestStep("Find the missing supply crates along the trail north", ObjectiveType.Collect, "supply", 3),
                new QuestStep("Bring the supplies back to Mira by the well", ObjectiveType.TalkTo, "mira")));
            QuestDefinition supplies = set.Supplies;
            set.Request = Phase1SceneBuilder.QuestAsset("Quest_VillagersRequest", q => q.Configure("villagers_request", "A Villager's Request",
                "Elder Rowan dropped his spectacles on the Eagle Ledge, halfway up Cloudpeak.", supplies,
                new QuestStep("Find Rowan's spectacles on the Eagle Ledge", ObjectiveType.Collect, "spectacles"),
                new QuestStep("Return the spectacles to Rowan", ObjectiveType.TalkTo, "rowan")));
            QuestDefinition request = set.Request;
            set.MountainClue = Phase1SceneBuilder.QuestAsset("Quest_MountainClue", q => q.Configure("mountain_clue", "The Mountain Clue",
                "Rowan's old map marks a shrine on the summit of Cloudpeak. Its carving holds a clue.", request,
                new QuestStep("Climb to the summit of Cloudpeak and inspect the shrine", ObjectiveType.Inspect, ShrineLandmark),
                new QuestStep("Light the three cairns in the order the carving shows", ObjectiveType.Inspect, CairnLandmark),
                new QuestStep("Tell Rowan what the cairns revealed", ObjectiveType.TalkTo, "rowan")));
            set.Feathers = Phase1SceneBuilder.QuestAsset("Quest_CloudFeathers", q => q.Configure("cloud_feathers", "Feathers of Cloudpeak",
                "Cloud feathers drift down the mountain. Gather them on the way to the summit.", null,
                new QuestStep("Gather the cloud feathers along the Cloudpeak Trail", ObjectiveType.Collect, "cloud_feather", 5)));
            return set;
        }

        /// <summary>A scene's QuestManager listing the given quests (tracker priority order).</summary>
        public static QuestManager AddManager(CollectibleRegistry registry, params QuestDefinition[] quests)
        {
            var manager = new GameObject("QuestManager").AddComponent<QuestManager>();
            var so = new SerializedObject(manager);
            SerializedProperty list = so.FindProperty("quests");
            list.arraySize = quests.Length;
            for (int i = 0; i < quests.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
            so.FindProperty("registry").objectReferenceValue = registry;
            so.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        // ───────────────────────────── Dialogue ─────────────────────────────

        internal static Phase1SceneBuilder.QuestLines MiraSupplies(QuestDefinition supplies) => new Phase1SceneBuilder.QuestLines
        {
            Quest = supplies,
            Offer = new[]
            {
                "Oh! A new face in Greenhaven. Welcome, traveller!",
                "Could you help me? Three supply crates fell off my cart on the trail north: one just past the bridge, the others in the Whispering Forest beyond the gate.",
                "Bring them back and I'll bake you something special!",
            },
            Active = new[] { "Any luck with the crates? Follow the road north over the bridge, then through the forest gate." },
            Complete = new[]
            {
                "You found all three! Greenhaven owes you one, explorer.",
                "Here, still-warm honey bread. Oh, and Elder Rowan was asking for you. He's on the bench by his house.",
            },
            Done = new[] { "Thanks again for the supplies! The whole village eats well tonight." },
        };

        internal static Phase1SceneBuilder.QuestLines RowanRequest(QuestDefinition request) => new Phase1SceneBuilder.QuestLines
        {
            Quest = request,
            Offer = new[]
            {
                "Mira tells me you're quite the finder of lost things!",
                "Last spring I climbed Cloudpeak as far as the Eagle Ledge, and my spectacles fell from my pocket there. Would you fetch them for an old man?",
            },
            Active = new[] { "The Eagle Ledge is halfway up the mountain, where the trail levels out. My spectacles should be by the bench." },
            Complete = new[]
            {
                "My spectacles! Now I can read the old map again...",
                "It marks a shrine at the very top of Cloudpeak. Let me look at it properly. Come back and talk to me again.",
            },
            Done = new[] { "The map is clearer now. Talk to me when you're ready for the summit." },
        };

        internal static Phase1SceneBuilder.QuestLines RowanMountainClue(QuestDefinition clue) => new Phase1SceneBuilder.QuestLines
        {
            Quest = clue,
            Offer = new[]
            {
                "Here: the map marks an old shrine on the summit, and three cairns around it.",
                "Climb to the top, read what is carved on the shrine, and do as it says. Then come and tell me what you find.",
            },
            Active = new[] { "The shrine is at the summit, past the ruined arch. Read the carving carefully; the cairns must be lit in its order." },
            Complete = new[]
            {
                "The cairns lit, and marks on the rock... an old trail beyond the summit!",
                "So the Lost Trail is real. You've done Greenhaven a great service, explorer. Rest now; the mountain will keep its secret a little longer.",
            },
            Done = new[] { "The Lost Trail... one day we'll follow it together. For now, enjoy the village." },
        };
    }
}
