# Technical Plan — Trails of Greenhaven MVP

## 1. Technical baseline
- Unity 6 LTS (choose one installed LTS version and pin it for the project).
- Universal Render Pipeline.
- C#.
- Unity Input System.
- Cinemachine compatible with the selected Unity version.
- CharacterController for player movement.
- Unity Test Framework for focused tests.
- Android build target from the beginning.

Before implementation, inspect the installed Unity version, project settings, packages, scenes, and available assets. Do not blindly upgrade or overwrite the project.

## 2. Proposed scene structure
Initial implementation:
- `Bootstrap` (optional if a simple single scene is enough)
- `GreenhavenRegion` (all MVP gameplay)

Start with one gameplay scene. Do not add additive region streaming until there is a demonstrated memory or world-size need.

## 3. Suggested source layout
Assets/_Game/
- Art/
- Audio/
- Materials/
- Prefabs/
- Scenes/
- ScriptableObjects/
- Scripts/Core/
- Scripts/Player/
- Scripts/Camera/
- Scripts/Interaction/
- Scripts/Quests/
- Scripts/NPC/
- Scripts/Collectibles/
- Scripts/Save/
- Scripts/UI/
- Tests/EditMode/
- Tests/PlayMode/

Create `ASSET_CREDITS.md` at project root for asset sources, licenses, and attribution obligations.

## 4. Core components
### Player
- `PlayerInputReader`: abstracts Input System actions and touch UI input.
- `PlayerMotor`: movement, gravity, jumping, grounded state.
- `PlayerAnimator`: maps movement state to Animator parameters.
- `PlayerInteractor`: requests interaction with the current target.
- `CameraRig`: configure Cinemachine follow/look-at and collision behaviour.

Keep movement logic independent from UI widgets. A UI joystick should feed the same movement abstraction used by keyboard/controller tests.

### Interactions
- `IInteractable`: exposes interaction availability, prompt text, and an interaction method.
- `InteractionDetector`: detects interactables in range using a bounded overlap or forward check; avoid uncontrolled repeated allocations.
- Interactables: `Collectible`, `NPCInteractable`, `LandmarkInteractable`.

The UI should display the prompt only for the currently selected valid target. Revalidate the target before interacting.

### Quest system
- `QuestDefinition` ScriptableObject: stable quest ID, title, description, and objective definitions.
- `QuestManager`: tracks active and completed quests.
- `QuestProgressData`: serializable runtime/save data.
- Quest events: use small explicit methods/events for objective progress. Avoid a global event bus unless actual complexity justifies it.
- Objective types for MVP: collect item, talk to NPC, inspect landmark.

### Collectibles
- Each collectible has a stable unique ID and item type.
- Collection updates the local world state and any applicable quest objective.
- World state must be restored from saved IDs.

### Save system
- Plain serializable save DTOs with a schema version.
- Local JSON file using Unity's persistent data path.
- Write defensively; validate loaded data and provide a new-game fallback.
- Consider writing to a temporary file and replacing the old save where supported.
- Do not store scene object references in JSON.
- Saving should occur on meaningful state changes and safe checkpoints, not every frame.

### UI
- Main menu or minimal start/continue flow.
- HUD with joystick, jump, interact, and objective tracker.
- Dialogue panel for NPC interaction.
- Mission completion feedback.
- Pause/resume.
- UI should scale across landscape resolutions.

## 5. Data flow
1. Input UI or device input provides movement/action intent.
2. `PlayerMotor` updates the character.
3. `PlayerInteractor` chooses a valid nearby interactable.
4. Interactable responds to explicit interaction.
5. Collectibles/NPCs/landmarks report relevant progress to `QuestManager`.
6. `QuestManager` updates objective and quest state.
7. UI observes quest state and refreshes the objective display.
8. `SaveService` serializes the updated plain-data state.

## 6. Performance approach
- URP with only required renderer features.
- Start with baked lighting and a single main light.
- Limit shadow distance/resolution and shadow-casting lights.
- Use LOD Groups where they help.
- Reuse modular environment props.
- Prefer simple colliders and limited physics.
- Keep vegetation density low enough for the minimum phone.
- Use compressed textures and appropriate import settings.
- Use object pooling only for objects that are frequently created/destroyed; do not add pooling complexity without a need.
- Profile on device at each milestone.

## 7. Testing
### Edit Mode
- Quest state transitions.
- Objective count/progress.
- Save DTO serialization and validation.
- Invalid/missing save fallback.
- Stable ID handling.

### Play Mode / manual
- Movement, jumping, slopes, and falling boundaries.
- Camera follow and obstacle behaviour.
- Interaction range and target changes.
- Collectible collected once only.
- Quest completion and persistence.
- App pause/resume.
- Touch layout on at least two aspect ratios if possible.

### Device performance
Record phone model, RAM, Android version, Unity build configuration, resolution, FPS/frame time, and observed thermal/memory issues.

## 8. Risks and mitigations
- **Assets do not match:** choose a single environment style and test a small scene before expanding.
- **Character/animation mismatch:** confirm rig compatibility and Animator setup before building gameplay around it.
- **Poor low-end performance:** profile early; reduce shadows, overdraw, and foliage before redesigning the entire world.
- **Scope creep:** freeze the MVP list; defer combat, multiplayer, procedural generation, and runtime AI.
- **Save corruption:** version save data, validate it, and provide a safe fallback.
- **Agent invents dependencies:** require the coding agent to report actual packages and files present.

## 9. Decision log

### 2026-10-11 — Phase 1 baseline
- **Unity version:** project is pinned to **6000.6.5f1 (Unity 6.6)** with URP 17.6.0 and Input System 1.20.0. The spec asks for "Unity 6 LTS"; whether 6.6 is an LTS stream must be confirmed before release. No upgrade or downgrade has been made.
- **Build target:** the default desktop platform (Windows standalone) is used for now, at the project owner's request. The Android Build Support module is **not installed** in this editor (only Windows and WebGL playback engines exist). Switching to Android is a later step (see `phase1-validation.md`).
- **Camera:** a custom `ThirdPersonCameraRig` (orbit, smooth follow, and sphere-cast collision, about 100 lines) is used instead of Cinemachine. Cinemachine is not installed, and adding it brings a new package and API surface without a Phase 1 need. Revisit if cinematic cameras or blends are needed.
- **Input:** `PlayerInputReader` defines its actions in code rather than reusing the template `InputSystem_Actions.inputactions`. The touch joystick and buttons use the Input System's `OnScreenStick` and `OnScreenButton`, which emulate a virtual gamepad, so touch, keyboard, and gamepad share one code path. Run = Shift, or a near-full stick push (≥ 0.9).
- **Touch camera look:** `TouchLookArea`, a drag region on the right of the screen. In the Editor, hold the right mouse button to look.
- **Player layer:** the player uses the built-in `Ignore Raycast` layer (2) so camera and interaction casts skip it without editing `TagManager.asset`. Replace this with a dedicated `Player` layer when custom layers are introduced.
- **Scene authoring:** `GreenhavenRegion.unity` is generated by the editor menu **Greenhaven ▸ Build Phase 1 Scene** (`Phase1SceneBuilder`), which places objects using a fixed seed. Generating the scene in the Editor avoids hand-writing scene YAML and keeps the slice reproducible. It is not runtime procedural generation. Once artists edit the scene by hand, stop re-running the builder or it will overwrite their work (it asks before replacing).
- **Assemblies:** `Greenhaven` (runtime), `Greenhaven.Editor`, and `Greenhaven.Tests.EditMode` asmdefs under `Assets/_Game`.
- **Interaction:** `DialogueInteractable` serves both NPCs ("Talk to") and readable props ("Read"). `PlayerInteractor` uses `OverlapSphereNonAlloc` every 0.1 s with a fixed buffer of 16.
- **Character (added later the same day):** Kenney *Animated Characters 1* (CC0) is imported as a **Generic** rig; all clips share one skeleton. Its stock skins are survivors and zombies, so a clean "adventurer" skin was made from the SVG source. `AdventurerImportSettings` (an AssetPostprocessor) controls the import settings. `PlayerAnimator` maps motor state to animation parameters and briefly ignores ungrounded moments (0.12 s) so steps don't trigger the jump pose. Known gaps: no walk clip, and no backpack (the spec's silhouette calls for one, so it will need a mesh attached to the spine bone).
- **Spec Kit:** `.specify/memory/constitution.md` now mirrors `trails-of-greenhaven-spec-kit/constitution.md` (v1.0.0). Amend both together.
- **Art pass (2026-10-11):** Quaternius CC0 packs were added: Stylized Nature MegaKit (Standard), Universal Animation Library (Standard) and Farm Animals. The Kenney character now imports as **Humanoid** so the library's clips retarget onto it, which replaces the Kenney idle/run/jump clips and fixes the missing walk. `ArtImportSettings` (AssetPostprocessor, versioned) holds all art import rules: 512 px texture cap, no normal maps, Humanoid for characters, Generic for animals. `GreenhavenArt` (editor) builds materials (URP Simple Lit; double-sided alpha cutout for foliage), animator controllers and the placement code. Each model is placed in a holder sized to a target height or width, so pack scale differences don't matter. Animals idle in place with `AnimatorRandomStart` and `CullCompletely`; there is no wandering AI yet. **Performance risk:** alpha-cutout foliage overdraw on low-end GPUs; counts are set in one place for tuning after device profiling.
- **Quests (2026-10-11):** `QuestDefinition` (ScriptableObject: stable id, title, optional prerequisite, ordered steps of Collect/TalkTo/Inspect), `QuestLog` (pure progress data, ready to save), `QuestManager` (scene owner, listens to `CollectibleRegistry`, raises Started/Progressed/Completed) and `QuestGiver` (NPC line selection by quest state). There is no global event bus: references are explicit and set in the Inspector. Collect objectives are evaluated against held counts rather than increments, so order of discovery doesn't matter. The supply counter was replaced by `QuestTrackerView` plus a `QuestToastView` banner.
- **Procedural asset generator, stage 1 (2026-10-11):** editor-only tool at **Greenhaven ▸ Generate Assets** (`Assets/_Game/Scripts/Editor/AssetGenerator/`, in the `Greenhaven.Editor` assembly). It generates faceted pine trees (Classic/Slender/Bushy), broadleaf trees (Round/Tall/Umbrella) and rocks (Boulder/Slab/Spire) into `Assets/_Game/Generated/` (Meshes, Materials, Prefabs, Preview). This follows the project's `_Game` convention instead of the brief's example `Assets/Greenhaven/Generated/`. All generated assets share **one** URP Simple Lit material that samples an 8×8 point-filtered palette texture. Per-face UVs select the colour, because URP Lit/Simple Lit ignore vertex colours. The output is deterministic: an independent random stream per (seed, category, variant), names `Gen_{Category}_s{seed}_v{nn}`, and settings stored in each mesh's importer `userData`, so **Validate** can regenerate and compare. For safety the tool only writes labelled `Gen_*` assets under `Generated/`, refuses paths occupied by unlabelled assets, confirms once before replacing, and replaces meshes in place to preserve GUIDs. Colliders are trunk capsules for trees and convex mesh colliders for rocks (80 triangles, under the 255 convex limit). Stage 2 (bushes and flowers) is pending: the palette rows are already reserved.
- **Phase 2: hero, village set and village scene (2026-10-11):**
  - **Hero** (`HeroGenerator`, **Greenhaven ▸ Generate Hero**): a lofted, faceted mesh (head and hair, face, tunic with belt, trousers, boots, red scarf, backpack with bedroll) smoothly skinned to a 21-bone skeleton named after `HumanTrait` bones, in T-pose facing +Z, about 1.74 m tall. `AvatarBuilder.BuildHumanAvatar` creates a Humanoid avatar, so the **existing** `Player.controller` (Universal Animation Library clips: idle, walk, jog, sprint, jump, interact) animates it with no new animation code. The Player builders prefer the hero, then the Kenney model, then primitives. Limits: mitten hands (no finger bones), rigid head and feet, simple linear joint blends, possible clothing intersection at extreme poses; a production rig would need hand-authored weights, finger bones and corrective shapes.
  - **Palettes:** a second 8×16 `VillagePalette` (skin, cloth, wood, plaster, roofs, stone, glass, water, path) with its own shared material. The original 8×8 nature palette is unchanged, so earlier assets keep their colours.
  - **Village set:** `BuildingGenerator` (Cottage, House, Shop: stone foundation and step, plaster walls, optional timber frame, thick gable roof with ridge cap, gables, framed door, windows with sills, shutters or flower boxes, chimney; the shop adds a display window, striped awning and hanging sign) and `PropGenerator` (well, bridge, fence, gate, bench, lantern, signpost, barrel, crate, planter). Bushes and flowers (stage 2) were added on the nature palette. Colliders: one box per building, a few boxes per prop, a non-convex mesh for the bridge, none for flowers and bushes.
  - **Village scene** (`VillageSceneBuilder`, **Greenhaven ▸ Create Village Scene** → `Assets/_Game/Scenes/GreenhavenVillage.unity`): hand-authored layout with a paved square and the well, six buildings facing it with paths to every door, two fenced gardens, a shop yard, house planters, lanterns, benches, readable signposts, a stream with invisible bank blockers so the bridge is the crossing, a gated north exit toward the Whispering Forest, hills plus an octagonal wall as boundaries, and seeded vegetation. Terrain, paths and water are generated meshes saved per seed. The builder validates building clearance, door access and the spawn. `GreenhavenRegion` is not modified; the new scene is not added to Build Settings.
  - **Controllers** are now rebuilt in place (same GUID), so building one scene no longer breaks another scene's controller reference.
- **Character polish (2026-10-11):** inspection showed a valid Humanoid rig already in use, so it was **reused** rather than replaced with transform-based animation. Hero **v2** (`Gen_Hero2_s{seed}`; v1 `Gen_Hero_s{seed}` kept as backup) has about 4.5-head storybook proportions, a smoother head, eye whites, pupils and highlights, brows, a smile, cheeks, layered hair tufts, a teal tunic with gold trim and laces, cream sleeves, gloves, belt hardware, detailed boots and a rounded backpack. Extra bones: humanoid `LeftEye`/`RightEye` and a non-human `Backpack`. `HeroSecondaryMotion` (runtime, visual only) adds blinking (eye-bone scale in LateUpdate), head and eye **look-at** toward the current interaction target (Animator IK; base layer IK Pass enabled) and a damped-spring backpack sway driven by acceleration. `PlayerMotor.FaceTowards` turns the hero toward an interaction target only while there is no steering input; physics, the controller and the camera are unchanged. A **Character Test scene** (`CharacterTest.unity`, own `Player_CharacterTest` prefab) shows looping showcase copies (idle, walk, jog, sprint, airborne), a talkable twin, a sign, jump blocks and a ramp. Main scenes pick up v2 only when rebuilt. Still missing for production quality: hand-authored weights, finger bones, facial blend shapes and cloth secondary motion on the tunic.
- **Owner-supplied adventurer model (2026-10-11):** `Greenhaven_Adventurer.obj` is static (not rigged) and uses **0-based OBJ face indices**, which Unity's OBJ importer would misread, so it stays outside `Assets/` (in `ArtSource/Characters/GreenhavenAdventurer/`). `AdventurerModelRig` (**Greenhaven ▸ Rig Greenhaven Adventurer Model**) parses it directly, straightens the arms into a T-pose (joints from each arm part's principal axis), scales it to 1.65 m, skins the named parts to the existing Humanoid skeleton layout (eye and backpack bones included), and builds a Humanoid Avatar plus a palette material from the MTL colours. The player builders now prefer this rigged model over the generated heroes; the generated heroes remain as fallbacks.
- **Weather, Phase 1 (2026-10-11):** `Greenhaven.Weather`: a `WeatherManager` with states Sunny/Cloudy/Rainy/Foggy (Cloudy is in the API but not yet tuned or tested), public `SetWeather(state, intensity?, seconds?)`, a `WeatherChanged` event, smooth smoothstep blends and optional automatic changes. **Sunny is the scene's own captured lighting**, restored exactly on disable or via `RestoreOriginal()`, so play sessions never corrupt scene settings (important with Enter Play Mode Options enabled). It drives sun intensity and colour, trilight ambient, **linear fog** (URP-supported and cheap) and the camera's solid background. There is no post-processing. `WeatherProfile` assets give scene-specific looks. `RainController` keeps one reused world-space particle system (default 600 max, 450/s) that follows the camera, uses stretched thin translucent streaks with a capped on-screen size, and supports optional looping audio (the project has no rain clip, so audio stays optional). Wind: trees are static-batched and cannot be moved at runtime, and wind sway needs a vegetation shader, so Phase 1 only publishes `_GreenhavenWind` as a global shader value. Editor: **Greenhaven ▸ Weather** (add to the open scene, then Sunny/Rainy/Foggy/Restore in Play mode) and inspector test buttons. Scene builders now add weather automatically.
- **Wind, stage 1 (2026-10-11):** a single `WindManager` (`Greenhaven.Wind`) simulates direction (with seeded wander), base strength, speed, seeded Perlin gusts and a smoothly advancing phase, and publishes `_GH_WindDirection`/`_GH_WindParams` as global shader vectors. It runs one Update for the whole world, with no per-plant scripts. It follows `WeatherManager.Wind` when present (the blended weather wind level, so Rainy raises wind gradually). `Greenhaven/WindLit` is a low-cost URP shader (Lambert + SH + main-light shadows + fog, SRP-Batcher-friendly CBUFFER, Forward/ShadowCaster/DepthOnly/DepthNormals passes) that bends vertices in **world space** using per-vertex wind data baked by the generators into **UV2** (x = bend, 0 at roots; y = flutter; z = frequency). World-space displacement keeps **static batching** valid, anchors roots by construction, and avoids cracks on faceted meshes (data is a function of position). Variation across plants comes from world-position phase. Pine, broadleaf and the new **Grass** generator use the shared `M_Gen_PaletteWind` material; rocks, buildings and props stay static. `effectScale` and the enable toggle give a cheap performance test. Next stages: bushes and flowers, wind-reactive signs and banners, leaf and petal particles, Android profiling. Unverified: GPU cost on device, and SRP Batcher compatibility (check in the Frame Debugger).
- **Canopy and wind v2 (2026-10-11):** Diagnosis of v1: bend depended only on height, so the whole crown moved as one mass; phase barely varied across a tree; motion was purely downwind; inner and outer canopy were treated alike; and the canopy was a few large blobs. **Broadleaf v2** has a flared trunk, branches reaching into the crown, a dark inner core, branch-tip clusters, 8–18 golden-angle shell clusters (biased to the top, smoother where visible) and low-poly underside filler, each with seeded size, rotation and shade. Wind data moved to a 4-channel UV2: trunk bend (coherent whole-tree sway), canopy weight (calm core to lively outer shell), per-cluster phase (clusters move as units, out of step) and flutter. Branches blend into their tip cluster so leaves never detach. The shader adds cross-wind orbiting, a gust-only canopy boost, an inner-canopy falloff and per-tree variation. **Per-type wind materials** (`M_Gen_Wind_Broadleaf/Pine/Grass`) allow tuning per tree type while still batching. `WindManager` gained gust duration (separate attack and release) and a Quality setting (High/Low/Off; Low drops flutter and halves cross-wind motion). `GeneratorSettings.FoliageDetail` (default 0.8) scales cluster count and smoothness. **Tree Comparison scene** (original v1, improved static, improved with wind). Pine and grass moved to the new data layout as an interim; the pine canopy rework follows after the owner signs off on broadleaf.
- **Breeze demo (2026-10-11):** `GreenhavenBreezeDemo.unity` (built by **Greenhaven ▸ Create Breeze Demo Scene**, own `Player_BreezeDemo` prefab, so no other scene changes) is a compact 48×48 m meadow with an S-curved path to a clearing, generated trees, grass, flowers, bushes and rocks, a bench, a readable signpost and a hanging sign, plus the existing player, camera and mobile HUD. The shared breeze reuses `WindManager`, which now drives every plant type: bushes and flowers got wind data (anchored at the ground) and their own shared materials. New lightweight components: `WindSwing` (hinged signs and banners, same sway curve as the shader), `LeafDrift` (one reused world-space particle system, at most 40 leaves, emission from gusts, carried along the wind direction), `AmbienceController` (optional wind and birdsong loops with fades; the project has no clips) and `BreezeDebugPanel` (F2: presets, gust, wind on/off, quality, leaves, ambience, animation readout). Character animation reuses the rigged Humanoid with mocap clips driven by the controller's real velocity and grounded state, plus `HeroSecondaryMotion` (blink, look-at, backpack sway); no procedural animation was needed. The builder checks for exactly one player, audio listener and wind manager.
- **Breeze in the village (2026-10-11):** `VillageSceneBuilder` now uses the breeze features through a shared editor helper, `AmbientMotionSetup` (also used by the breeze demo): leaf drift, optional ambience, the hanging `WindSwing` sign, the F2 panel (hidden at start in the village) and a wind-readiness check. Before building, it lists plant types whose prefabs for the seed still use the static material and offers to regenerate them in place (confirmed; Skip keeps them static). The vegetation scatter adds meadow grass and grass with occasional flowers along every path edge (both scale with tree density and respect the layout and the stream). The hanging sign stands beside the main road at the first free candidate spot. Wind and weather were already in the scene; wind follows weather. `GreenhavenRegion` is unchanged.
- **Phase 3: Whispering Forest, Cloudpeak Mountain and Summit Trail (2026-10-11):**
  - **Scene choice:** `GreenhavenVillage` (76 × 76 m, hand-authored layout) and `GreenhavenRegion` are too small to extend safely, so the forest and mountain are a **separate scene, `CloudpeakAdventure.unity`**, built by **Greenhaven ▸ Create Cloudpeak Adventure Scene** (parameters in **Greenhaven ▸ Cloudpeak Generator…**). It continues from the village's gated north exit ("toward the Whispering Forest"): the trail starts at a gate in a gap in the boundary hills on the south edge, and its sign points back to the village. There is no scene-loading trigger yet; both scenes are standalone until a scene-flow system exists (see tasks).
  - **Terrain approach:** a custom faceted mesh rather than Unity Terrain, to match the flat-shaded palette art of the village. `CloudpeakWorld` (editor assembly, no Unity objects, unit-tested) defines the natural height field: a rolling forest floor; a two-tier mountain (foothill skirt, a level shelf at mid-height, a steep summit cone, a flat summit plateau); rocky spurs radiating from the summit (Ridge Variation); boundary hills around the edge with an opening at the entrance; and the Whispering Brook. Default 200 × 200 m region, 44 m summit, 2 m grid (about 20 k terrain triangles in 16 chunks with mesh colliders). Same settings and seed give the same world.
  - **Trail:** one continuous route, built in sections: a Catmull-Rom S-curve through the forest and the Whispering Glade, straight across a bridge over the brook, then **constant-grade climbing** on the mountain. The climbing path is steered at the angle off the contour that gives the target grade on the mountain profile, so it hugs the natural slope. Traverses on the skirt reverse at semicircular **hairpins** (switchbacks), a near-level traverse along the shelf passes the Eagle Ledge viewpoint, a ledge spirals round the summit cone, and a straight final approach runs through the ruined arch. Heights are smoothed and grade-limited both ways (default limit 0.25 ≈ 14°; the controller climbs 45°). The terrain is then **carved**: level across the trail and a margin beside it (wider on steep ground), with a smooth shoulder back to natural ground. Clearings, rest stops, the viewpoint and the summit are flattened platforms. The visible trail mesh samples the carved surface exactly (same triangulation), so it neither floats nor clips. The walkable surface is the terrain collider itself.
  - **Population:** vegetation density varies by region (dense forest, sparse foothills, small pines on gentle mountain ground, open summit). Tall trees are kept out of the view corridors from the glade and the rest stop to the summit banner. Trees stay 2.5–3 m from the trail edge and grass 0.3 m. New nature generator **ForestPropGenerator**: fallen logs (Mossy/Broken/Long), stumps (Low/Tall), mushroom clusters (Toadstool/Brown/Golden), static and on the nature palette. Cliff rocks go on steep ground (never on the trail), boulders frame the climb, distant snow-capped peaks give depth (no collision), and the summit has a ruined arch, a shrine, a cairn with a wind-swung banner and an overlook wall.
  - **Exploration and wind:** Whispering Glade, Foothill Rest, Eagle Ledge and Cloudpeak Summit, with readable signs along the route. Five Cloud Feathers use the existing collectible and quest systems; a tiny new runtime `QuestAutoStart` starts the "Feathers of Cloudpeak" quest without a quest giver. Reuses `WindManager`, the wind materials, `LeafDrift`, ambience and the F2 panel through `AmbientMotionSetup`; there is one wind manager and no per-plant scripts.
  - **Performance:** small plants, logs and boulders get single-level LOD groups that cull them when tiny on screen; terrain and trail are static-batched; there are no real-time lights beyond the sun; fog hides the far edge. The Vegetation Density and Grid Cell settings are the main levers for low-end phones. The tree budget is the biggest risk: about 175 trees at density 1, and broadleaf trees can reach about 2 k triangles each. Device profiling is pending.
  - **Validation built into the builder:** data checks (continuity, walked grade on the carved surface, cross-slope, terrain slope under the trail, cut and fill, no trail section running beside another, summit reached); a physics sweep for colliders standing on the trail; terrain line-of-sight from the glade and the rest stop to the summit banner; and a **walk test**, in which a CharacterController with the player's exact settings is steered along the trail at run speed with gravity and no jumping, from the entrance to the summit. Results are printed in the build report.
  - The village builder now generates only missing asset types (adding logs, stumps and mushrooms to the library no longer prompts a full regeneration).
- **Linked world (2026-10-11):** the village and Cloudpeak are joined as **linked scenes** (the owner's choice over one large scene: lighter on Android memory and smaller changes). New runtime `Greenhaven.World`: `SceneGate` (a trigger; when the player walks in it fades the screen, loads the target scene and records an arrival id), `SceneArrival` (runs before the player's Awake, so the player appears there, the motor records it as the respawn point and the camera starts behind the player) and `ScreenFader` (a black overlay kept across the load; it blocks touch input during the change). Statics are reset for Enter Play Mode Options. The village's north gate leads to the Cloudpeak forest entrance, and that entrance gate leads back. Both scene builders add the links, and **Greenhaven ▸ Link Village and Cloudpeak** adds them to already-built scenes and sets the build list (village first, so the game starts there). Limitation: collectibles and quest progress are per scene and reset when a scene reloads, until save/load (Phase 6) exists.
- **Trail tuning:** the trail is level where it crosses a clearing, rest stop, viewpoint or the summit, and grade-limited around them. The level margin beside the trail widens on steep ground, the switchbacks reverse every 43°, and the minimum mountain radius is 60 m with the peak at most 0.75 × radius. In-memory trail checks pass for the default and twelve other combinations (seeds 7/2026/99, radius 55→60/80, peak 70→52.5/20, half-size 85/130, grade 0.2, ridge 1, cone 0.7): steepest walked grade ≤ 15°, cross-slope ≤ 14.3°, terrain under the trail ≤ 28°, no overlaps, summit reached.
- **Wildlife (2026-10-11):** original procedural animals: fox (the quality benchmark), deer (stag/doe), rabbit, goat and bird (bluebird/robin/finch), generated by editor code (`Scripts/Editor/Wildlife`) as **rigidly skinned meshes**. Each animal is one SkinnedMeshRenderer on one shared palette material (`M_Gen_WildlifePalette`), about 600–1,000 triangles with 10–15 bones; colours, proportions and variation live in `WildlifeGeneratorSettings`. Animation is **procedural** (`AnimalMotion`), not Animator clips: no clips exist for these bodies, and transform animation keeps them light and tunable. It covers gait-blended legs whose cycle follows the real ground speed, look and graze, ears, tail, hops and flight. Behaviour (`WildlifeAgent`) is a small state machine. One `WildlifeManager` runs decisions at staggered, distance-based rates (no Update per animal), pauses animals beyond 70 m, animates only visible ones, and keeps animals off trails through blocked cells rasterised from the path meshes. Movement is kinematic over walkable ground (terrain and non-convex mesh colliders; rocks are convex, so they count as obstacles), validated by a few raycasts per destination. No navmesh package was added. Animals have no colliders. Species tuning assets are created once and never overwritten. Placement (`Populate Current Scene`, also run by the Cloudpeak builder) is deterministic, undoable, and clears only its own root. Audio hooks are empty because the project has no animal clips. See `Assets/_Game/Scripts/Wildlife/README.md`.
- **Flowing rivers (2026-10-11):** inspection found two flat, unanimated water strips (the village Stream and the Cloudpeak Whispering Brook) with palette UVs unsuitable for flow, URP 17.6 without Shader Graph, and no audio assets. New custom shader `Greenhaven/RiverWater` (no textures, no new packages). It uses two ripple layers driven by the two-phase flow-map technique (no stretching or seams, whatever the per-segment speeds), a turquoise-to-deep gradient with darker banks, derivative-based highlights and sparkles, and foam masked per vertex. Low quality is opaque with one layer and no specular. River meshes are smooth spline strips with lengthwise UVs in metres; foam, signed segment speed (blended at boundaries) and depth are baked into UV2, so nothing is rebuilt at runtime. `RiverFlowController` (runtime) handles quality, pooled floating leaves and rock splashes (one particle system each), and one optional ambience source that follows the listener along the river, all paused when far away. Editor: Generate Flowing River (upgrades existing water and only hides the old mesh), Rebuild Selected River, an Inspector with Scene-view control points, bank details (pebbles, plants, rocks in the shallows; kept off trails, paths and bridges), Create River Demo Scene, and Capture River Preview. The village and Cloudpeak builders now create flowing water. See `Assets/_Game/Scripts/Environment/RIVER_README.md`.
- **Adventurer v2 (2026-10-11):** guided by the owner's turnaround sheet (`Adventurer Character Turnaround Sheet.png`). `AdventurerModelRig` builds a second prefab, `Gen_Adventurer2`, from the same OBJ; v1 (`Gen_Adventurer`) is untouched and remains the fallback. Proportions are reshaped in model space (no uniform scaling): legs about 20 % longer with a gentle taper and fuller trousers, a narrower torso with a defined waist (shoulders keep their width), a slimmer and slightly longer neck, and arms 6 % longer and 10 % slimmer. Overall height is 1.84 m (+12 %), and the head stays slightly oversized. Bones and skin weights follow the same height remap. Reference colours: forest-green jacket, olive trousers, tan boot soles, and bare forearms and hands with rolled cream cuffs and leather wristbands. Face: thicker, higher brows on their own bones, a wider smile on a mouth bone, and thin upper eyelids on the eye bones (they close with a blink). A mustard scarf (loop plus two tails) swings on a two-bone chain. The Animator, controller and mocap clips are unchanged, so walk, run, jump, land, interact, pick-up, emote and talk keep driving the body. `HeroSecondaryMotion` (one component, v1-compatible) adds randomised blinks with occasional doubles, small eye movements, idle curious glances (IK look-at with a raised brow), a happy expression after interacting, collecting or finishing a quest, a surprised brow on jumps, mouth movement while talking, and scarf sway from acceleration, speed and wind. The player builder refits the capsule to the model height and frames the camera for taller characters.
