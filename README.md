# Shadow Steps (Unity project "StealthEscape")

Original portrait, turn-based stealth puzzle for Android/iOS. Unity 2022.3.62f3 + URP.

- Rules, levels and solutions: `reference/` (authoritative) — see `MASTER_GAME_SPEC.md`, `IMPLEMENTATION_PLAN.md`, `CLAUDE.md`.
- Code: `Assets/_ShadowSteps/Scripts/{Core,Data,Runtime,UI,Editor}`; tests in `Assets/_ShadowSteps/Tests`.
- Packaged level data: `Assets/StreamingAssets/ShadowSteps/levels.json` (must stay identical to `reference/levels.json`).
- Progress, decisions and known issues: `Docs/`.

## Quick checks
- `Tools/run_core_tests.sh` — builds Core + Data + EditMode tests with the dotnet SDK and runs them (no Unity needed).
- `python3 -I Tools/gen_meta.py` — creates deterministic `.meta` files for new assets.
- Unity: Window ▸ General ▸ Test Runner (EditMode and PlayMode).
