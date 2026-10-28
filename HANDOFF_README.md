# Shadow Steps — Claude Code COMPLETE V2 Handover

**Key revisions:** square grid nodes with explicit adjacency graph and uniform distance; strictly top-down orthographic 2D sprite presentation. Existing URP 3D project can be retained if working.

## For a Claude Code task already running
1. Let current write/test operation finish safely; do not run simultaneous agents against same workspace.
2. Back up / commit known-good work using your usual repository policy.
3. Put this package in the Unity project root (`CLAUDE.md` alongside `Assets/` and `Packages/`).
4. Send Claude `prompts/MIGRATE_EXISTING_PROJECT_V2.txt`.
5. Require a migration audit and small tested increments, not wholesale replacement.

## For a new Unity project
Start with `CLAUDE.md` and `V2_CHANGE_CONTROL.md`, then `V2_IMPLEMENTATION_ROADMAP.md`. Do not use the legacy START_HERE prompt without V2 override.

## Priority
`V2_CHANGE_CONTROL.md` > `LEVEL_DESIGN_PARAMETERS.md` > `V2_IMPLEMENTATION_ROADMAP.md` > `MASTER_GAME_SPEC.md` > original V1 reference docs and solver (legacy-only).

## Important disclaimer
The 20 supplied levels and validation traces were generated for earlier V1 grid assumptions. They are useful source material, not proof that the migrated V2 node-graph levels are solved. New traces must be run.
