# Shadow Steps V2 — Incremental implementation roadmap

## M0 Audit and checkpoint
Inspect current work, dependencies and tests; write `Docs/V2_MIGRATION_AUDIT.md`, capture git diff/backup. Gate: no lost source, clear file-by-file compatibility assessment.

## M1 Pure C# graph gameplay
Add versioned nodes + explicit directed connections, uniform node positions, legal movement & route enforcement, edge type validation and deterministic move/facing intervals. Preserve prior turn manager if compatible. Gate: EditMode tests on valid/invalid graph edges and guard states pass.

## M2 2D orthographic rendering
Keep same logical board. Render a top-down 2D tile/sprite board, paths and nodes; animated sprite player/guards, cone overlays, occluders and exits. Camera orthographic with portrait fit. Gate: no movement distances vary; vision overlays match logical detection.

## M3 Level schema/editor migration
Implement `schemaVersion:2` and explicit edge authoring, one-way & locked edge tools, patrol path checks, sight blocker editing and JSON round-trip; convert V1 levels via a versioned migration command with backup. Gate: editor serializes and loads the same logic; legacy import behaves as documented.

## M4 Rebuild first 5 tutorial levels
Use first-five design in `LEVEL_DESIGN_PARAMETERS.md`, but author exact node graphs; run solver with gadget restrictions where indicated. Gate: five freshly verified traces, replay tests pass, no fabricated 'optimal' labels.

## M5 Revalidate all 20 legacy levels
Convert/modify designs to fit V2 graph and visual readability. Re-solve from scratch for V2, record shortest paths and mechanic necessity where relevant. Gate: 20/20 V2 solvable with actual trace logs.

## M6 Presentation and UX
Polish top-down 2D art, guard states, selection, HUD, audio, portrait safe area, feedback and animations, without changing rules. Gate: entire game loop playable without debug controls.

## M7 QA and release
Run performance and device checks; build Android test AAB and iOS export as supported; leave store credentials/legal decisions to human. Gate: recorded editor/device evidence and publish checklist.

**Working strategy:** implement one testable slice at a time; don't delete previously working logic or start a fresh Unity project. No claiming an automated solver proves a level fun or intuitively readable.
