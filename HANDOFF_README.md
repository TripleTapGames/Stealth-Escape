# Shadow Steps — handoff to Claude Code

## What to do now
1. Create/open a **Unity URP 3D project** (recommended Unity 2022.3.62f1 if starting fresh; keep existing project configuration otherwise).
2. Extract this folder into the ROOT of the Unity project — the same directory containing `Assets/`, `Packages/`, and `ProjectSettings/`. Its `CLAUDE.md` should be at the root (move the folder contents to root if necessary).
3. In VS Code open the **Unity project root** and open a Claude Code session.
4. Paste `prompts/START_HERE.txt` as the first Claude Code instruction.
5. Allow Claude to implement milestone by milestone; use `prompts/RESUME.txt` next time and `prompts/QA_AUDIT.txt` for an independent audit.

## Important
- `reference/` contains the previously generated actual original 20 levels + canonical Python rules/solver. They are **not just example sketches**.
- Run `python3 reference/build.py` only if you intend to regenerate levels and validation. Back up reference files first.
- Instruct Claude to **copy** `reference/levels.json` to `Assets/StreamingAssets/ShadowSteps/levels.json` and use this packaged JSON as the authoritative initial content.
- You can build an MVP without final AI art; Unity primitives and original modular geometry are sufficient until the game works.
- Unity engine installation/editor configuration, Android signing, iOS codesigning, store assets, privacy policy and actual publishing require user actions/credentials. Claude should prepare workflows without claiming it has completed restricted steps.

## Files
- `CLAUDE.md`: persistent Claude Code behavior and constraints.
- `MASTER_GAME_SPEC.md`: detailed full product and architecture blueprint.
- `IMPLEMENTATION_PLAN.md`: eight milestones and go/no-go gates.
- `reference/`: original previous complete level design, data, solution validation and Python reference solver.
- `prompts/`: ready-to-paste start/resume/audit instructions.
