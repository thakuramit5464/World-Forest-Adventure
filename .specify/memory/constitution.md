<!--
Sync Impact Report
- Version change: (unfilled template) → 1.0.0
- Source: trails-of-greenhaven-spec-kit/constitution.md (authoritative project constitution)
- Principles defined:
  I. Product Identity
  II. Mobile-First Performance
  III. Offline-First
  IV. Small, Playable Increments
  V. Maintainable Architecture
  VI. Art and Licensing
- Added sections: Reliability and Safety; Definition of Done; Governance
- Removed sections: none (template placeholders replaced)
- Templates requiring updates: none modified by this command
  (plan/spec/tasks templates read the constitution at runtime)
- Follow-up TODOs: none
- Note: keep trails-of-greenhaven-spec-kit/constitution.md and this file in sync;
  amend both together.
-->

# Trails of Greenhaven Constitution

## Core Principles

### I. Product Identity

Trails of Greenhaven is a stylized, animated-movie-inspired, third-person exploration adventure
for Android. The player controls a young human explorer travelling through forests, mountains,
villages, and mysterious ruins. Exploration, environmental puzzles, NPC conversations, and
missions MUST remain the core experience.

### II. Mobile-First Performance

- The minimum target is a representative entry-level Android phone with 4 GB RAM; the exact
  device baseline MUST be recorded during testing.
- Target stable 30 FPS on the minimum device in representative gameplay.
- Performance MUST be profiled on physical devices; Editor performance is not a substitute.
- Prefer URP, baked lighting where suitable, modest texture sizes, LODs, simple colliders,
  limited real-time shadows, and restrained transparency/overdraw.
- Costly visual effects MUST NOT be added unless profiling shows adequate headroom.
- Memory, battery, thermal behaviour, and loading time are in scope.

### III. Offline-First

- Core exploration, missions, collectibles, and saving MUST work without an internet connection.
- Required accounts, cloud services, analytics, ads, or remote APIs MUST NOT be introduced into
  the MVP.
- Any future network feature MUST be optional and MUST NOT block offline play.

### IV. Small, Playable Increments

- Build one complete playable slice before expanding the world.
- Every implementation phase MUST produce a testable result.
- Speculative systems or large frameworks MUST NOT be implemented without a current MVP need.
- Prioritize movement feel, camera behaviour, clear interactions, and a satisfying discovery
  loop.

### V. Maintainable Architecture

- Use C# and clear separation of responsibilities.
- Avoid a single "manager" or MonoBehaviour containing unrelated game systems.
- Use ScriptableObjects for static definitions such as quests and item metadata; store mutable
  player progress in serializable save data.
- Prefer interfaces and small components for interactions.
- Avoid unnecessary per-frame allocations, global mutable state, and hidden dependencies.
- Use dependency references that are visible and configurable in the Inspector where practical.

### VI. Art and Licensing

- Use a consistent stylized 3D storybook look with expressive characters, rounded forms, matte
  materials, and coherent palettes.
- Prefer free assets with clear licenses for the prototype. Each third-party asset, source,
  license, and attribution requirement MUST be recorded in `ASSET_CREDITS.md`.
- Do not assume an asset is free for commercial use without checking its license.
- Do not copy protected characters, worlds, logos, or distinctive designs from existing
  franchises.

## Reliability and Safety

- Handle missing Inspector references gracefully and provide actionable warnings.
- Saving MUST tolerate missing, invalid, or older save files.
- Never claim that code compiles, a build succeeds, or a device test passes unless it was
  actually verified.
- Keep player controls usable across common Android aspect ratios and touch layouts.
- Do not overwrite existing project files without inspecting them first.

## Definition of Done

A task is complete only when:

1. Its implementation is present.
2. Relevant automated checks or manual test steps are documented.
3. The project compiles, if compilation was available to verify.
4. Remaining manual setup, asset dependencies, and unverified behaviour are stated clearly.
5. `tasks.md` is updated honestly.

## Governance

- This constitution is authoritative over the spec, plan, and tasks. If a proposed change
  conflicts with it, the conflict MUST be explained and the project owner's approval obtained
  before the principle is changed.
- Amendments are made in `trails-of-greenhaven-spec-kit/constitution.md` and mirrored here in
  the same change, with the version and Last Amended date updated.
- Versioning follows semantic versioning: MAJOR for removing or redefining a principle, MINOR
  for adding a principle or materially expanding guidance, PATCH for clarifications and wording.
- Every phase review checks compliance with these principles, in particular performance
  evidence, offline behaviour, asset licensing, and honest task status.

**Version**: 1.0.0 | **Ratified**: 2026-10-11 | **Last Amended**: 2026-10-11
