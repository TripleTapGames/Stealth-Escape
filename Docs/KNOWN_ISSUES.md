# Shadow Steps — known issues and unverified items

| # | Status | Area | Description | Workaround / next step |
|---|---|---|---|---|
| 1 | open | tooling | Unity Editor GUI is open on this project, so `-batchmode` compile/test runs are locked out. All Unity-side verification (asmdef import, Test Runner, scenes) is pending. | Close the editor and run `Tools/run_unity_tests.sh` (added in M2), or run Window ▸ General ▸ Test Runner manually. |
| 2 | design | content | Level 14 "Smoke and Mirrors" is solvable in 11 moves without smoke; EMP is never strictly required on levels 12, 15, 18, 20 (constrained solver). Levels are intentionally unchanged. | Human playtest decides whether these levels still teach their mechanic; tutorial text must not claim EMP is required. |
| 3 | design | content | Guard routes cross the door tile in levels 10 and 19 and the key tile in level 16 (validator warnings). Legal per the reference rules. | Presentation must keep key/door readable under a guard. |
| 4 | open | settings | `defaultScreenOrientation: 4` (Auto Rotation) in ProjectSettings; portrait lock is applied in M8 with the other player settings. | — |
| 5 | info | tooling | `Tools/run_core_tests.sh` uses NUnit 3.14 from NuGet (network needed once); Unity bundles NUnit 3.5. Only standard NUnit 3 APIs are used in tests. | If a test behaves differently under Unity, treat the Unity result as authoritative. |
