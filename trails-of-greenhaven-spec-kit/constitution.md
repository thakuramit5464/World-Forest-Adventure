# Project Constitution — Trails of Greenhaven

## 1. Product identity
Trails of Greenhaven is a stylized, animated-movie-inspired, third-person exploration adventure for Android. The player controls a young human explorer travelling through forests, mountains, villages, and mysterious ruins. Exploration, environmental puzzles, NPC conversations, and missions are the core experience.

## 2. Mobile-first performance
- The minimum target is a representative entry-level Android phone with 4 GB RAM; the exact device baseline must be recorded during testing.
- Target stable 30 FPS on the minimum device in representative gameplay.
- Profile on physical devices; Editor performance is not a substitute.
- Prefer URP, baked lighting where suitable, modest texture sizes, LODs, simple colliders, limited real-time shadows, and restrained transparency/overdraw.
- Do not add costly visual effects unless profiling shows adequate headroom.
- Keep memory, battery, thermal behaviour, and loading time in scope.

## 3. Offline-first
- Core exploration, missions, collectibles, and saving must work without an internet connection.
- Do not introduce required accounts, cloud services, analytics, ads, or remote APIs into the MVP.
- Any future network feature must be optional and must not block offline play.

## 4. Small, playable increments
- Build one complete playable slice before expanding the world.
- Every implementation phase must produce a testable result.
- Do not implement speculative systems or large frameworks without a current MVP need.
- Prioritize movement feel, camera behaviour, clear interactions, and a satisfying discovery loop.

## 5. Maintainable architecture
- Use C# and clear separation of responsibilities.
- Avoid a single “manager” or MonoBehaviour containing unrelated game systems.
- Use ScriptableObjects for static definitions such as quests and item metadata; store mutable player progress in serializable save data.
- Prefer interfaces and small components for interactions.
- Avoid unnecessary per-frame allocations, global mutable state, and hidden dependencies.
- Use dependency references that are visible and configurable in the Inspector where practical.

## 6. Art and licensing
- Use a consistent stylized 3D storybook look with expressive characters, rounded forms, matte materials, and coherent palettes.
- Prefer free assets with clear licenses for the prototype. Record each third-party asset, source, license, and attribution requirements in `ASSET_CREDITS.md`.
- Do not assume an asset is free for commercial use without checking its license.
- Do not copy protected characters, worlds, logos, or distinctive designs from existing franchises.

## 7. Reliability and safety
- Handle missing Inspector references gracefully and provide actionable warnings.
- Saving must tolerate missing, invalid, or older save files.
- Never claim that code compiles, a build succeeds, or a device test passes unless it was actually verified.
- Keep player controls usable across common Android aspect ratios and touch layouts.
- Do not overwrite existing project files without inspecting them first.

## 8. Definition of done
A task is complete only when:
1. Its implementation is present.
2. Relevant automated checks or manual test steps are documented.
3. The project compiles, if compilation was available to verify.
4. Remaining manual setup, asset dependencies, and unverified behaviour are stated clearly.
5. `tasks.md` is updated honestly.
