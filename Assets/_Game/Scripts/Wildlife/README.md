# Greenhaven Wildlife

Original, procedurally generated animals for *Trails of Greenhaven*. Everything is built in Unity by our own
code: no Blender, no downloaded or paid models, and no imported animation clips.

| Prefab | Look | Behaviour |
|---|---|---|
| `GreenhavenFox` | Orange-red, cream muzzle/chest/tail tip, dark socks and ear tips, big two-bone bushy tail | Wanders quiet forest edges, forages, investigates, trots and flees |
| `ForestDeer` | Tan with cream belly/throat/rump; v00 stag with antlers, v01 doe | Grazes in clearings, looks up at the player, flees with tail raised |
| `ForestRabbit` | Compact grey-brown, long ears, big haunches, cotton tail | Hops near bushes, nose twitch, bolts when startled |
| `CloudpeakGoat` | White (v00) or grey (v01), curved horns, beard, amber eyes | Grazes on ledges, curious and bold, slow to flee |
| `ForestBird` | Bluebird, robin or finch (v00–v02) | Perches, pecks, flutters; flies between perches when disturbed |

## Making and placing them

1. **Greenhaven ▸ Wildlife ▸ Generate Wildlife**: builds every species and variant. **Generate Fox Only** builds just the fox (the quality benchmark).
2. **Greenhaven ▸ Wildlife ▸ Capture Previews**: renders a contact sheet (side, three-quarter and front views) to `Generated/Wildlife/Preview/`.
3. **Greenhaven ▸ Wildlife ▸ Populate Current Scene**: places animals in the open scene under a `Generated Wildlife` object.
   - Undo (Ctrl+Z) removes them again, so you can preview a placement before keeping it.
   - **Clear Generated Wildlife** removes only that object.
   - `CloudpeakAdventure` is also populated automatically when it is rebuilt.
4. **Greenhaven ▸ Wildlife ▸ Create Wildlife Demo Scene**: builds `Scenes/WildlifeDemo.unity`, a meadow with every species in its habitat.

## Where things are

- `Assets/_Game/Generated/Wildlife/Prefabs/` holds the prefabs, named `GreenhavenFox_v00` and so on.
- `.../Meshes/` holds the skinned meshes, and `.../Preview/` the contact sheets.
- `Generated/Materials/M_Gen_WildlifePalette.mat` is the **one shared material**. It uses a 8 × 14 point-filtered palette texture, the same approach as the trees and buildings.
- `.../WildlifeGeneratorSettings.asset` holds the colours, proportions (fox tail, deer legs, rabbit ears, goat horns, overall scales), size and colour variation, variant counts and the seed. Edit it, then generate again. The same settings always give identical animals.
- `.../Species/WildlifeSpecies_*.asset` holds the behaviour and animation tuning for each species:
  - movement: speeds, turn rate, wander radius, maximum slope;
  - awareness: detection, flee, personal-space and calm distances;
  - behaviour: wander and graze chances, curiosity, boldness range;
  - animation: stride length, leg swing, grazing angle, hop height, wing beat rate;
  - optional calls.

  These are created once and **never overwritten** by regeneration.
- Code lives in `Scripts/Wildlife/` (runtime) and `Scripts/Editor/Wildlife/` (generator, placement, demo, previews).

Regeneration only replaces assets carrying the generator's label, asks first, and keeps their GUIDs, so placed animals update in place.

## How it works

- **Model:** lofted, faceted parts skinned *rigidly* (one bone per part) to about 10–15 bones: body, neck, head, ears, tail, legs, and wings for birds. Each animal is one skinned renderer with one material, so one draw call, and about 600–1,000 triangles.
- **Animation (`AnimalMotion`):** procedural and transform-based.
  - Legs blend smoothly between walk, trot and gallop phase patterns. The cycle rate follows the actual ground speed, so feet barely slide.
  - Also covered: body bob and breathing, look-around and look-at head turns, grazing (neck and head lowered), ear flicks, tail sway (the deer's tail flags when fleeing), rabbit hops, and bird flapping, wing flutters and pecks.
  - Only bones below the root move. The root belongs to the behaviour.
  - Wind adds a few degrees to ears and tails only; animals do not sway like trees.
- **Behaviour (`WildlifeAgent`):**
  - States: Idle → Wander / Graze → Look at player / Investigate → Flee → Return. Birds use Perched ⇄ Flying.
  - Each animal has its own boldness and reaction delay, so they don't all react on the same frame.
  - Destinations are checked with a few raycasts, only when one is picked: the ground must be walkable (terrain or a non-convex mesh collider), not too steep, with no rocks, trunks or props there, and a clear run to it.
  - Trails and paths are baked into *blocked cells* that animals avoid; they may cross only while fleeing.
  - Animals have **no colliders**, so they never push or block the player.
- **Manager (`WildlifeManager`, one per scene):** runs decisions at 10 Hz up close, about 3 Hz further away and about 1 Hz at the edge, staggered across animals. It pauses animals beyond Active Distance (70 m), animates only visible ones, and makes no per-frame allocations.
  - Inspector settings: **Wildlife Enabled**, **Density** (0–1, a deterministic subset), **Quality** (Low halves the update rates and skips ears, tail and wind detail), distances.
  - Two pooled audio sources play optional calls, at most two at a time with a cooldown.

## Testing

- **F4** (Editor and development builds): live list of nearby animals with state, distance and boldness.
- **Walk slowly** to get close: deer graze and look up, goats may investigate.
- **Run**, and you're noticed from further away; animals flee sooner.
- Birds on the fence or rocks fly to another perch when you approach.
- EditMode tests: `WildlifeTests`.

## Not yet verified

Play Mode behaviour has not been watched by a person yet, and nothing has been profiled on an Android device. Check frame time with about 20 animals in `CloudpeakAdventure` on a low-end phone, and lower Density or Quality if needed.

## Audio

The project has no animal sounds. Each species has an empty **Calls** list; add CC0 clips there (and record them in `ASSET_CREDITS.md`) and they play quietly and occasionally near the player.
