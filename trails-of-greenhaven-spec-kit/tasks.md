# Implementation Tasks — Trails of Greenhaven

Mark tasks complete only after implementation and appropriate verification.

## Phase 0 — Project inspection and baseline
- [ ] Record Unity version and installed packages.
- [ ] Confirm URP project and Android build support.
- [ ] Inspect current scenes, scripts, input settings, and assets.
- [ ] Choose one representative low-end Android test device.
- [ ] Create project folder structure and `ASSET_CREDITS.md`.
- [ ] Make a baseline Android build and record any existing failures.

**Exit criteria:** project state is understood, the baseline build status is documented, and asset licenses are tracked.

## Phase 1 — Player movement slice
- [ ] Import one rigged humanoid character.
- [ ] Configure Animator Controller and required animation parameters.
- [ ] Implement input abstraction.
- [ ] Implement walking, running, gravity, jumping, and grounded checks.
- [ ] Configure third-person camera follow and collision.
- [ ] Add landscape mobile controls.
- [ ] Build and test on a physical Android device.

**Exit criteria:** player movement and camera feel reliable; no major control blockers.

## Phase 2 — First environment
- [ ] Create a compact forest trail and viewpoint.
- [ ] Add terrain, trees, rocks, and one landmark.
- [ ] Set up basic lighting and materials.
- [ ] Add collision and safe world boundaries.
- [ ] Tune LODs, shadows, and foliage density.
- [ ] Measure device frame time.

**Exit criteria:** one visually coherent path is explorable at the performance target or has documented optimization issues.

## Phase 3 — Interaction and collectibles
- [ ] Define `IInteractable`.
- [ ] Implement interaction detection and contextual prompt.
- [ ] Implement one collectible prefab with stable ID.
- [ ] Prevent duplicate collection.
- [ ] Update a visible counter or objective display.
- [ ] Test interaction range and repeated taps.

**Exit criteria:** player can reliably discover and collect items on touch devices.

## Phase 4 — Quest system
- [ ] Create quest definitions using ScriptableObjects.
- [ ] Implement quest states and objective progress.
- [ ] Implement collect-item objective.
- [ ] Implement talk-to-NPC objective.
- [ ] Implement inspect-landmark objective.
- [ ] Build the three MVP missions.
- [ ] Add mission start, progress, and completion UI.

**Exit criteria:** all three missions can be completed in a predictable sequence.

## Phase 5 — NPCs and world flow
- [ ] Add at least two interactable NPCs.
- [ ] Add dialogue UI with short text.
- [ ] Add the village start point.
- [ ] Connect village, forest, and mountain trail.
- [ ] Add one route/landmark unlocked by progress.
- [ ] Check that mission objectives are understandable without external instructions.

**Exit criteria:** the region supports a complete short adventure loop.

## Phase 6 — Save/load and offline validation
- [ ] Define versioned save DTOs.
- [ ] Persist quest states and objective counts.
- [ ] Persist collected stable IDs and unlock state.
- [ ] Restore player position safely.
- [ ] Handle missing, malformed, and older save data.
- [ ] Test app restart and airplane mode.
- [ ] Test app pause/resume and Android back navigation.

**Exit criteria:** core progress survives restart and core gameplay needs no network.

## Phase 7 — Polish and performance
- [ ] Tune mobile UI for common landscape aspect ratios.
- [ ] Add basic audio feedback where available.
- [ ] Profile CPU/GPU frame time on target device.
- [ ] Optimize textures, shadows, foliage, and draw calls based on evidence.
- [ ] Check memory and thermal behaviour during a longer play session.
- [ ] Run regression tests for movement, interactions, quests, and saves.
- [ ] Record known issues and create a release candidate build.

**Exit criteria:** acceptance criteria in `spec.md` are checked and all remaining limitations are documented.

## Deferred backlog — not MVP
- [ ] Larger connected world or additive region loading.
- [ ] More villages, ruins, and story chapters.
- [ ] Day/night or weather.
- [ ] Advanced NPC schedules.
- [ ] Combat, crafting, or large inventory.
- [ ] Runtime AI-generated missions.
- [ ] Multiplayer or cloud save.
