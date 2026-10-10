# AGENTS.md

Instructions for AI agents working on **Throughput**. Read this first, then the docs it points to.

## What this project is

**Throughput** is a solo learning project: a 3D queue-engineering puzzle game about an airport security checkpoint.
- The player **plans** a checkpoint layout in a toy-diorama view, presses **Run**, then **watches and manages** it live.
- A real queueing-theory simulation runs underneath.
- Main learning subjects: **queueing theory** and **game design**. Unity, art and code polish may take shortcuts.
- No deadline. Learning comes first; shipping (itch.io) is a bonus.

**Current phase:** design is done for the most part; there's **no game code yet**. Next step: the spec for sub-project 1 (simulation core + validation). See [docs/roadmap.md](docs/roadmap.md).

## Where things are

| Path | What it holds |
|---|---|
| [docs/roadmap.md](docs/roadmap.md) | The 4 sub-projects, their order and "done when" checks. Check the current status here. |
| [docs/game-mechanism/decisions.md](docs/game-mechanism/decisions.md) | Decisions D1–D12: play style, passengers, simulation time, space, success, random events… |
| [docs/game-mechanism/mechanism-scopes.md](docs/game-mechanism/mechanism-scopes.md) | Mechanisms by role: passenger behaviour, player tools, difficulty, feedback, hidden math, costs |
| [docs/game-mechanism/knowledge/](docs/game-mechanism/knowledge/) | Queueing knowledge base: 25 concepts (HTML with diagrams) and the research report with sources |
| [docs/game-design/pillars.md](docs/game-design/pillars.md) | Design pillars P1–P4: **every new idea must pass them** |
| [docs/game-design/style.md](docs/game-design/style.md) | Visual style: two views, camera, colour, characters, UI, motion |
| [Throughput Design System](https://claude.ai/artifact/Ux7qp2fzRbByeqNN4DHrMH) | Exact colours, sizes, type, UI components (private claude.ai artifact) |
| [docs/architecture/](docs/architecture/) | Code architecture A1–A4, simulation core, viewer and playback |
| [Unity/Docs/best-practices.md](Unity/Docs/best-practices.md) | Unity best practices (folders, naming, .meta, git, asmdefs, C# style) and how Throughput follows them |
| `docs/superpowers/specs/` | Approved specs (one per sub-project) |
| [docs/throughput-brainstorm.md](docs/throughput-brainstorm.md) | The **original** idea notes. Several parts are superseded by the decisions; the decision docs win. |

## The decisions agents most often need

- **Plan → Manage (D4):** layout is frozen during a run. Live manager actions only: routing, open/close lane, move staff, lane rules, signs. **1× speed only**, with pause. Scrubbable replay after the run.
- **No result preview (D11).** **Random but fair events (D12):** a fixed event deck per level, random timing per attempt.
- **Simulation:** discrete-event (event list), an **abstract queueing network** (not 3D space), **simulate first then replay**, **re-simulate from now** on manager actions.
- **`Throughput.Core` never references Unity.** It must stay plain C#, testable headless.
- **Determinism:** run seed + plan + timed manager actions must always produce the same timeline. Use separate random streams per source. Never use frame time or real time in the simulation.
- **Pillars:** P1 every fix has a cost · P2 the cause is always visible · P3 fair, not lucky · P4 calm, not frantic.
- **Visuals:** green/yellow/red are reserved for status and always paired with ● ▲ ■. Accent is belt blue. Passengers are told apart from above (hats, hair, bags), never by status colours.

## How to work with the user

- **Debate, don't just agree.** The user asked for a debating partner. Challenge contradictions, explain trade-offs, and give a recommendation.
- **One question at a time.** Offer 2–4 lettered options (A/B/C), a short trade-off table if useful, and say which one you recommend and why.
- **Plain, simple English.** Short sentences, concrete examples, no jargon without a one-line explanation. The user may not be a native English speaker.
- **One concept per section.** When explaining, use the pattern: what it is → a simple diagram or example → how it's used in Throughput. Don't merge several concepts into one block.
- **Document decisions as they're made**, in the right file (decisions, pillars, style, architecture). Update the summary tables and add a change-log row; keep a short "History" note when a decision is revised.
- **When something changes, update everything it touches**: decisions, pillars, scopes, architecture, style, and the design system if it's affected.
- Visual explanations (HTML pages, look tests) are welcome. Label prototypes as **throwaway** and keep them in `docs/**/prototypes/`.

## Rules for changes

- **No implementation code before the sub-project's spec is approved**, and then its implementation plan. Follow the order in [docs/roadmap.md](docs/roadmap.md).
- **Check every new feature against the pillars** (the 4-question check in [pillars.md](docs/game-design/pillars.md)) and the **pairing rule**: every difficulty needs a matching player tool, and every tool needs a difficulty.
- **Scope:** ask "can we ship without it?" before adding anything. Prefer adding a module over changing the core.
- **Git:**
  - Commit only when the user asks.
  - Solo project: commit and push directly to `main`. Create a branch only when the user asks for one.
  - Short, clear commit messages.
- **Environment:** Windows; Git Bash and PowerShell are both available. Unity 6 LTS + C# when code starts.
  - **Unity CLI** (`%LOCALAPPDATA%\Unity\bin\unity.exe`) drives the open editor through the Pipeline package: check `unity status`, then use `unity command …` rather than hand-editing scene/asset YAML. Pass C# to `eval` from Bash, not PowerShell (PowerShell splits the string). See [Unity/Docs/best-practices.md](Unity/Docs/best-practices.md) §11.

## Planned code layout (when code starts)

All code lives in the Unity project (`Unity/Assets/Core`, `Unity/Assets/Game`), created by Unity 6000.3.26f1, never by hand. See [docs/architecture/project-structure.md](docs/architecture/project-structure.md).


| Assembly | Contains | May reference |
|---|---|---|
| `Throughput.Core` | Level, Plan, layout compiler, simulation core, timeline, results | Nothing from Unity |
| `Throughput.Core.Tests` | EditMode tests: unit, Erlang C/A validation, determinism and balance | `Throughput.Core` |
| `Throughput.Game` | Unity views, playback, input, plan editor, UI | `Throughput.Core` |
