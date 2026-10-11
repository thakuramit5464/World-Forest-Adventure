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
