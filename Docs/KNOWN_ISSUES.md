# Shadow Steps — known issues and unverified items

| # | Status | Area | Description | Workaround / next step |
|---|---|---|---|---|
| 1 | open | tooling | Unity Editor GUI is open on this project, so `-batchmode` compile/test runs are locked out. All Unity-side verification (asmdef import, Test Runner, scenes) is pending. | Close the editor and run batch mode, or run Window ▸ General ▸ Test Runner manually (expected: 169 EditMode tests pass). |
| 2 | design | content | Measured with constrained solves on the migrated legacy topology: smoke is strictly required on 11, 17, 19, 20 only; level 14 "Smoke and Mirrors" is solvable in 11 moves without smoke; EMP is never strictly required (12, 15, 18, 20); WAIT is strictly required on 2, 5, 9, 10, 17, 20. | V2 M5 decides per level whether to redesign; tutorial copy must not claim a mechanic is required where it is not. |
| 3 | design | content | Guard routes cross the door node in levels 10 and 19 and the key node in level 16 (validator warnings). Legal under D-011. | Presentation must keep key/door readable under a guard. |
| 4 | design | content | Level 18 has two guards sharing node (2,0) at phases 1, 5, 7, 11. Tolerated as a legacy warning (D-012); a schema-2 level with the same data would be rejected. | Redesign in V2 M5. |
| 5 | open | settings | `defaultScreenOrientation: 4` (Auto Rotation) in ProjectSettings; portrait lock is applied with the other player settings in the release milestone. | — |
| 6 | info | tooling | `Tools/run_core_tests.sh` uses NUnit 3.14 from NuGet; Unity bundles NUnit 3.5. Only standard NUnit 3 APIs are used. | Unity result is authoritative if they ever differ. |
| 7 | info | engine | Guard cycle period uses the legacy formula; guards with `moveEvery` ≠ `rotateEvery` are new territory with unit tests but no shipped content yet. | Author such guards only through the editor with validation. |
| 8 | open | content | The 20 legacy levels are **not** V2-validated as shipped content. They load, migrate and reproduce V1 results on the equivalent topology, but V2 traces (`validation_v2.json`) are produced only in V2 M5 after any redesign. | Do not present legacy validation.json as V2 proof. |
| 9 | design | content | Tutorial levels 104 (12 moves) and 105 (14 moves) exceed the "Tutorial 4–9 actions" target band; 102 has a unique shortest solution. Mechanic necessity is proven; pacing is a playtest decision. | Revisit after human playtest; shorten with the level editor and re-export traces. |
| 10 | info | tooling | The random wall search (`--search`) keeps authored edges and only adds walls; it cannot propose new one-way/locked edges or guard routes. | Author those by hand in the Level Editor. |
