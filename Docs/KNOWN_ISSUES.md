# Shadow Steps — known issues and unverified items

| # | Status | Area | Description | Workaround / next step |
|---|---|---|---|---|
| 1 | open | tooling | Unity Editor GUI is open on this project, so `-batchmode` compile/test runs are locked out. All Unity-side verification (asmdef import, Test Runner, scenes) is pending. | Close the editor and run batch mode, or run Window ▸ General ▸ Test Runner manually (expected: 169 EditMode tests pass). |
| 2 | design | content | Measured with constrained solves on the migrated legacy topology: smoke is strictly required on 11, 17, 19, 20 only; level 14 "Smoke and Mirrors" is solvable in 11 moves without smoke; EMP is never strictly required (12, 15, 18, 20); WAIT is strictly required on 2, 5, 9, 10, 17, 20. | V2 M5 decides per level whether to redesign; tutorial copy must not claim a mechanic is required where it is not. |
| 3 | design | content | Guard routes cross the door node in levels 10 and 19 and the key node in level 16 (validator warnings). Legal under D-011. | Presentation must keep key/door readable under a guard. |
| 4 | resolved | content | Level 18 guard overlap: fixed in the shipped schema-2 pack by moving patrol A to row 1 (D-021); the V1 reference file keeps the original data as a legacy warning. | — |
| 5 | open | settings | `defaultScreenOrientation: 4` (Auto Rotation) in ProjectSettings; portrait lock is applied with the other player settings in the release milestone. | — |
| 6 | info | tooling | `Tools/run_core_tests.sh` uses NUnit 3.14 from NuGet; Unity bundles NUnit 3.5. Only standard NUnit 3 APIs are used. | Unity result is authoritative if they ever differ. |
| 7 | info | engine | Guard cycle period uses the legacy formula; guards with `moveEvery` ≠ `rotateEvery` are new territory with unit tests but no shipped content yet. | Author such guards only through the editor with validation. |
| 8 | resolved | content | The shipped 20 levels now carry fresh V2 traces in `validation_v2.json` (C# solver) replayed by PackagedPackTests. `reference/validation.json` remains V1-only evidence. | Keep `validation_v2.json` regenerated whenever levels.json changes. |
| 9 | design | content | Tutorial levels 104 (12 moves) and 105 (14 moves) exceed the "Tutorial 4–9 actions" target band; 102 has a unique shortest solution. Mechanic necessity is proven; pacing is a playtest decision. | Revisit after human playtest; shorten with the level editor and re-export traces. |
| 10 | info | tooling | The random wall search (`--search`) keeps authored edges and only adds walls; it cannot propose new one-way/locked edges or guard routes. | Author those by hand in the Level Editor. |
