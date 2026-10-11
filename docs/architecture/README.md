# Architecture

How Throughput's code is organised.

| File | What's in it |
|---|---|
| [overview.md](overview.md) | Decisions A1–A4 (event-list simulation, simulate-then-replay, abstract queueing network, assemblies), the big-picture diagram and the role of each piece. Start here. |
| [simulation-core.md](simulation-core.md) | Inside `Throughput.Core` (`Core/Engine/`): network nodes, event loop and event types, blocking, policies (player decisions), behaviours (passenger modules), observers, seeded random streams, time. |
| [project-structure.md](project-structure.md) | Repo layout across Core, Unity and Blender; A5: engine inside the Unity project with a no-engine-references asmdef; how to create the Unity project. |
| [viewer-playback.md](viewer-playback.md) | Inside `Throughput.Game`: the run step by step (1×, pause, manager actions applied live, results, replay), rules, pieces, random events in the viewer. |

**Coming next:** data formats, testing.

**Related:**
- [../game-mechanism/](../game-mechanism/): what the simulation must model
- [../game-design/](../game-design/): pillars and visual style the viewer must follow
