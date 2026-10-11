# Trails of Greenhaven — Spec Kit Starter

A lightweight, Spec-Driven Development starter for a stylized 3D exploration adventure built with Unity 6 and C# for entry-level Android phones.

## What's included

- `constitution.md` — non-negotiable project principles
- `spec.md` — product requirements and MVP scope
- `plan.md` — technical design and architecture
- `tasks.md` — phased, testable implementation checklist
- `CLAUDE_START_PROMPT.md` — prompt to give Claude Code after adding these files

## Recommended workflow

1. Put these files in the root of your Unity project, preferably in a `/specs` folder or a separate `/project-spec` folder.
2. Ask your coding agent to inspect the existing project and read all files before making changes.
3. Implement only Phase 0 / Phase 1 tasks first. Do not ask the agent to implement the entire game at once.
4. After each phase, build the Android app, test on a physical device, and update `tasks.md`.
5. Keep this spec authoritative. If a proposed change conflicts with `constitution.md`, explain the conflict and ask before changing the principle.

## If you use GitHub Spec Kit

This package is a lightweight project-specific spec pack; it is not the official GitHub Spec Kit CLI scaffold. You can copy the requirements into the official `/speckit.specify`, `/speckit.plan`, and `/speckit.tasks` workflow if you use that tooling.
