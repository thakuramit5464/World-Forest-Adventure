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
