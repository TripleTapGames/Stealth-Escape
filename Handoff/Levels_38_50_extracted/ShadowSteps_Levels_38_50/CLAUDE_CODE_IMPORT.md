# Claude Code — import Shadow Steps LEVEL-38 to LEVEL-50 (candidates)

I have supplied `ShadowSteps_Levels_38_to_50_Design_Handoff.zip`. Unzip it in a temporary work folder.

1. Read `README_START_HERE.md` and `LOCAL_VALIDATION.json` before editing the Unity project.
2. Inspect the actual Unity stage importer, existing stage registry, graph validator, deterministic C# solver, Ghost/score policies, camera projection and occlusion, and art/theme configuration. Do not infer code paths or APIs.
3. Treat `Levels_38_to_50_Candidates.json` as **13 NEW levels only** (LEVEL-38...LEVEL-50). `base:"LEVEL-30"` is only an inheritance/example template: **DO NOT OVERWRITE LEVEL-30 OR 1–37**. 36/37 were previously separately handed off; leave them unchanged.
4. Preserve all supplied graph layouts, patrol positions, camera mounts, doors, keys, gadgets and code/title/intro text. If a level fails to import/solve or an edge is misinterpreted, REPORT the precise mismatch and proposed correction BEFORE editing geometry.
5. Re-run the **authoritative C# validator/solver** for ALL 13 levels. Verify guard schedules, no overlapping guards, no illegal move or WAIT; camera 70° width, 3-grid-square reach from wall face; before/after visibility; locked-door occlusion; two-turn Smoke/EMP duration and alarm final-action rule.
6. Prove or disprove these *provisional* claims with the real solver: Level43 mandatory Smoke, Level46 mandatory EMP, Level50 BOTH mandatory Smoke and EMP, keys on 41/45/47/49/50 mandatory; knockout shorter than no-KO on 38,40,42; 39 offers a longer clean route.
7. Compute official optimal actions, 3-star/2-star thresholds, valid Ghost availability, and flag any uninteresting/unneeded cameras or guards. Do NOT copy legacy LEVEL-30 scoring or Ghost field from `base`; compute from new level.
8. Integrate background art (4 visual chapters), stone walls and bush planters at logical `#`/`X` cells, natural wall/pole camera mounting. Do not alter gameplay obstruction based on decorative art. Portrait viewport must pan horizontally only for 15-column levels; pan gestures consume no turns.
9. Deliver a validation report and screenshots for LEVEL-43, LEVEL-46, LEVEL-49 and LEVEL-50 with overlaid vision, before marking candidate set accepted. Stop and request approval if solver results contradict the local report. No silent level-redesign.
