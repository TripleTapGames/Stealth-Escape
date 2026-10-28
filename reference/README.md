# Shadow Steps — prototype package

Files:
- `DESIGN.md`: authoritative game rules, Unity architecture, board diagrams, exact shortest action sequences, build milestones and Codex/Claude prompt.
- `levels.json`: 20 original board definitions.
- `validation.json`: solver-certified winning shortest action sequences and state counts.
- `build.py`: procedural source and BFS reference solver; run with `python build.py` to deterministically regenerate both JSON files. It requires only Python 3 standard library.

Validation means each board is solvable and the reported path is shortest given **the reference rules**; does not imply all theme mechanics are mandatory. Do not change rules in Unity without revalidating.
