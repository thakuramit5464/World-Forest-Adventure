# Product Specification — Trails of Greenhaven MVP

## 1. Summary
Create a small, polished, offline-first 3D exploration game for Android. The player is a young human adventurer who explores a stylized valley, discovers landmarks, helps villagers, collects objects, and solves a simple environmental puzzle.

## 2. Vision
The game should feel like walking through a colourful animated adventure film while remaining technically viable on entry-level Android phones. Visual identity comes from art direction, composition, animation, colour, and environmental storytelling—not expensive realism.

## 3. Target audience and platform
- Platform: Android phones, landscape orientation.
- Minimum performance target: stable 30 FPS on a defined entry-level device, initially represented by a 4 GB RAM phone.
- Input: touchscreen first; keyboard/controller support may be used in Editor testing.
- Connectivity: no network required for core gameplay.
- Engine: Unity 6 LTS, URP, C#.

## 4. Core gameplay loop
1. Explore a trail or village.
2. Notice a landmark, character, object, or environmental clue.
3. Interact with an NPC or object.
4. Receive a mission or discover an objective.
5. Travel, collect items, or solve a simple puzzle.
6. Return or reach a destination to complete the objective.
7. Receive a clear reward and unlock the next point of interest.

## 5. Player character
The player controls a young human explorer with a backpack and a distinctive, readable silhouette.

### Required actions
- Walk and run.
- Jump and land.
- Turn and move relative to the camera.
- Interact with nearby objects and NPCs.
- Pick up collectibles.
- Trigger simple contextual actions.

### Control requirements
- Virtual joystick on the left side of the screen.
- Contextual interaction/action button on the right.
- Jump button on the right.
- UI must respect safe margins and work on common landscape aspect ratios.
- Controls should not require pixel-perfect taps.

## 6. Camera
- Third-person follow camera.
- Smooth follow and configurable distance/height.
- Prevent obvious clipping through terrain and large obstacles where feasible.
- Keep the player visible during normal movement and jumps.
- Avoid camera shake or cinematic effects in the initial MVP unless explicitly triggered.

## 7. World and MVP boundaries
Build one connected, compact region with three visual sub-areas:
1. **Greenhaven Village:** start point, 2–3 NPCs, tutorial and mission introduction.
2. **Whispering Forest:** winding trail, vegetation, landmarks, and collectibles.
3. **Cloudpeak Trail:** rising path, a simple obstacle/puzzle, and a scenic viewpoint.

These are working names. The MVP is not a giant seamless world. It is a small handcrafted region that feels explorable through winding paths, elevation, sightlines, and points of interest.

### World interaction
- Walkable terrain and clear boundaries.
- At least 3 recognizable landmarks.
- Collectibles that visibly respond to interaction.
- At least one route or gate that becomes accessible after mission progress.
- Environmental props should be reused and placed intentionally.

## 8. NPCs and missions
### NPC requirements
- At least 2 NPCs in the MVP.
- Interactable when within range and when the player presses the interaction button.
- Display a short dialogue line or dialogue panel.
- NPCs may remain stationary in the MVP; wandering AI is optional.

### MVP missions
1. **The Missing Supplies:** find 3 supplies along the forest trail.
2. **A Villager's Request:** speak to an NPC and deliver or locate a named item.
3. **The Mountain Clue:** inspect a landmark and activate a simple environmental puzzle.

### Quest behaviour
- Quest definitions are data-driven.
- Quest states include NotStarted, Active, Completed; Failed may be added only if needed.
- Objectives update clearly when progress changes.
- Completion feedback is shown to the player.
- Completed quest state persists after restarting the app.

## 9. Collectibles and rewards
- At least one collectible type.
- Collection triggers visible feedback and updates quest progress where relevant.
- Collected items do not respawn after loading a saved game if they were already collected.
- A simple counter or quest tracker displays required progress.
- Full inventory management and crafting are out of scope.

## 10. Save/load
Save locally on device. At minimum persist:
- Save schema version.
- Player position and rotation, if the location is safe to restore.
- Collected item IDs.
- Quest states and objective progress.
- Relevant unlocked route/landmark state.

Requirements:
- Handle missing or invalid save files without crashing.
- Provide a default new-game state when no valid save exists.
- Avoid serializing Unity object references directly; save stable IDs and plain data.
- Document how save data is versioned and tested.

## 11. Visual and audio direction
### Visual style
- Stylized 3D storybook / animated-film inspiration.
- Rounded environmental shapes and clear silhouettes.
- Matte, coherent materials and limited texture complexity.
- Forest greens, warm village earth tones, and cool mountain blues.
- Distant scenery may use simplified geometry or background silhouettes.
- Avoid photorealistic assets mixed with cartoon assets unless intentionally stylized to match.

### Audio
- Basic footsteps or interaction sound effects where available.
- Ambient environment loop is optional for the first playable slice.
- Audio must be controllable through a master volume setting if settings UI is implemented.

## 12. Performance requirements
- Target 30 FPS on the defined minimum device in representative gameplay.
- Use URP and profile CPU/GPU frame time on device.
- Use LODs for suitable models, baked lighting where practical, simple colliders, reasonable texture sizes, and limited real-time shadows.
- Avoid dense transparent foliage, excessive particles, unnecessary post-processing, and large numbers of dynamic lights.
- No unbounded spawning or repeated per-frame allocations in core loops.
- Record device model, Android version, graphics settings, FPS/frame time, memory observations, and test scene.

## 13. Accessibility and usability
- Clear interaction prompts.
- Readable objective text on a phone screen.
- Controls are consistently positioned.
- Do not communicate essential mission instructions only through colour.
- Provide a pause/resume path and handle Android app focus changes without corrupting progress.

## 14. MVP out of scope
- Multiplayer or co-op.
- Combat system.
- Procedural infinite world.
- Open-world streaming across many scenes.
- Online account, cloud save, or required API.
- AI-generated missions at runtime.
- Complex inventory, crafting, economy, or skill trees.
- Fully voiced dialogue.
- Day/night simulation and advanced weather.
- Monetization.

## 15. Acceptance criteria
The MVP is accepted when:
1. A player can launch the game and start or continue a local save.
2. Touch controls support movement, running, jumping, and interaction.
3. The camera follows the player without major clipping during normal movement.
4. The player can explore the village, forest, and mountain trail in one connected playable region.
5. At least 2 NPCs can be interacted with.
6. The three specified missions can be completed.
7. Collectibles and mission progress update correctly.
8. Completed missions and collected items persist across app restarts.
9. Core gameplay works in airplane mode.
10. A physical Android test is performed and its result is recorded. Performance target is measured, not assumed.
11. Build errors, known defects, asset licensing notes, and manual setup requirements are documented.
