# Roadmap: Sub-projects

The full design is too big for one build. It's split into sub-projects, each with its own **spec → implementation plan → build** cycle, done in order. Each one ends with something that works and can be checked.

| # | Sub-project | What it delivers | Done when | Status |
|---|---|---|---|---|
| 1 | **Simulation core + validation** | The queueing engine in plain C# (`Throughput.Core`): network, event loop, policies, behaviours, run seeds, timeline, results. No Unity. | Strict tests match M/M/1, Erlang C/A and P-K within 3%, Kingman within 15%; Little's Law and invariants hold on every run; same seed + actions → same timeline ([spec + tracker](superpowers/specs/2026-10-11-phase-1-core-design.md)) | Spec approved; next: plan |
| 2 | **Greybox playable** | Unity viewer and plan editor: one level with capsules, Plan → Run at 1×, pause, manager actions, re-simulate from now, results, replay | One level is playable from plan to results; the find-the-fun gate: 3 testers, at least 2 want "one more" | Later |
| 3 | **Look and feel** | Design-system UI, blueprint map, characters, lighting, motion | One level looks and feels final | Later |
| 4 | **Content** | Levels, event decks, unlockable passenger behaviours, progression, perceived wait | A full run from menu to final level | Later |

**Rules:**
- A sub-project starts only after the previous one meets its "done when".
- If something big is discovered mid-way, update this file and the affected docs before continuing.

**Source material for the specs:**
- [game-mechanism/](game-mechanism/)
- [game-design/](game-design/)
- [architecture/](architecture/)
