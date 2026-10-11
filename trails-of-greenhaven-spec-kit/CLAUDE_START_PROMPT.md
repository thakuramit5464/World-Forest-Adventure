# Prompt for Claude Code

Read `constitution.md`, `spec.md`, `plan.md`, and `tasks.md` completely before changing anything.

You are helping build **Trails of Greenhaven**, a stylized 3D exploration adventure in Unity 6 LTS / URP / C# for entry-level Android phones.

## Required behaviour
1. First inspect the actual repository and Unity project: version, packages, scenes, scripts, input setup, assets, and current build status.
2. Summarize the current state and propose the smallest next implementation step.
3. Follow `constitution.md` as non-negotiable project rules.
4. Implement only Phase 0 and Phase 1 from `tasks.md` unless I explicitly approve more.
5. Do not assume assets, packages, animation clips, or Animator Controllers exist; verify what is present.
6. Do not overwrite existing files without inspecting them and explaining why changes are needed.
7. Prefer a small, maintainable implementation over a broad framework.
8. If a Unity build or device test cannot be run in your environment, say so plainly and give me the exact verification steps.
9. Update `tasks.md` only for work that is actually completed and verified.
10. Keep the game offline-first and mobile-performance-focused.

Start by inspecting the project and presenting:
- Existing project status
- Missing dependencies/assets
- Proposed file changes
- Tests and verification available
- Any decisions that need my approval

Do not generate the entire game at once. Work in small, testable increments.
