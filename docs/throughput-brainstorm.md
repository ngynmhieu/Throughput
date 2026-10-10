# Throughput: Brainstorm Notes

Solo learning project. A 3D, drag-and-click game about engineering queues. The player designs the rules, routes and staffing for a queue, runs the simulation, and checks whether the design holds up.

---

## 1. Project name history

| Name | Result |
|---|---|
| Queue Commander | Fits an airport admin only, too tied to one setting |
| Queue Lab | Good, but a generic name |
| Next, Please | **Rejected.** Already a $5.99 Steam game about checkout anxiety (Feeling Potion, Nov 2025), plus a browser rhythm game on itch.io |
| Flowline | **Rejected.** "Flowlines VS." is an existing puzzle game (Baltoro Games, 2019, Switch/Xbox), and "flowline" is also a puzzle genre name |
| **Throughput** | **Chosen.** No matching game surfaced in search. Still check the Steam store and itch.io search by hand |

**Suggested subtitle:** "Throughput: Queue Engineer" (helps search, explains the game).

**Repo name:** `throughput` (lowercase, hyphens if needed).

---

## 2. Game concept

**Pitch:** The player is a queue engineer. Each level is a queue problem with a goal and a budget. They build the system, set the rules, press Run, and watch whether the design works.

**Setting:** Airport security checkpoint for v1. Later: hospitals, package networks, streets, and any other place with a queue.

**Core loop:** read the problem → design and calculate → run the simulation → compare the prediction with the result → optimize → earn stars.

**Shorter round loop (3–5 min per wave):**
1. Observe: a flight bank or demand spike arrives.
2. Diagnose: the slowest stage glows red.
3. Act (drag and click): place barriers, open/close lanes (costs staff), move staff, set priority lanes, place wait-time signs, divert flow to another checkpoint.
4. Watch the result.
5. Score: wait time, happiness, missed flights, staff cost → 1–3 stars.
6. Upgrade: choose 1 of 2 offers.

**Fail states:** happiness hits zero, or too many passengers miss their flights (reneging).

**Design principle:** every fix has a cost. Lanes cost staff, barriers take space, priority helps one group and slows another.

---

## 3. What the player controls

| Layer | Player decides | Math behind it (hidden from the player) |
|---|---|---|
| Structure | One shared line or separate lines; number of servers; multi-stage flow | Pooling, M/M/c, bottleneck = slowest stage |
| Capacity | Staff per stage, and when | Utilization ρ = λ / (c·μ); Erlang C for waits |
| Discipline | FIFO, priority, shortest job first, express lane for ≤1 bag | Scheduling theory (shortest job first lowers average wait; priority has a fairness cost) |
| Routing | Where each arrival goes: shortest line, least work, random, by type | Join-shortest-queue, load balancing |
| Dynamic policy | Rules like "open lane 3 if the queue is over 20" | Threshold control, hysteresis (stops lanes flickering on and off) |

**Rule/function editor (late game):** players start with presets, then write small rules, e.g.
- route(p) = lane with min(queueLength × avgServiceTime)
- openLane when waitEstimate > 8 min for 30 s

This is a Zachtronics-style puzzle: design the logic, then watch it run.

---

## 4. Hiding the math

- Players don't need formulas, but they do need to know **why** the queue got worse. A jump with no explanation feels like cheating.
- Show results in plain words: "Average wait: 8 min", "Lane 3 is idle".
- Diagnosis in plain words: "Checkpoint 2 is the bottleneck: 3 people waiting per staff member".
- Goals in plain words: "Keep waits under 8 minutes", not a target utilization.
- Put formulas in an optional **"Why?" panel or glossary** after a level.
- Before each run, show "Expected wait: about 6 min". Move the formula to the Why panel.

---

## 5. Queuing concepts as mechanics

| Concept | Game mechanic | Simplification |
|---|---|---|
| Arrival rate λ | Flight board, arrival banks | Scripted spike curve per flight, not pure random |
| Service rate μ | Lane speed depends on staff skill and passenger type | Base time × passenger-type multiplier × small jitter |
| Servers c | Opening lanes costs staff | Show ρ as a gauge; red above 100% (queue grows forever) |
| Little's Law L = λW | Signs show "≈ W min" = queue length ÷ recent throughput | Rolling 60-second throughput so it feels live |
| Pooling | One serpentine line vs separate lines | Make the difference big and visible |
| Queue discipline | Priority lane toggle | Shows the trade-off in the score |
| Balking | Agents divert or complain | Threshold depends on visible queue length |
| Reneging | Patience bar → missed flight | Patience depends on how close the flight is |
| Bottleneck | Pipeline stages; diagnose overlay | Adding staff upstream of the bottleneck does nothing (a key "aha") |
| Variability | Random rescans, "liquids" passengers | A "standardize" upgrade reduces variance |

**Late-game twist:** dedicated lanes can sometimes beat pooled lines in real life, because staff take ownership of "their" customers (a 2015 Management Science study of an emergency department found this). Use it as an optional mechanic.

**Real-world anchors for tuning:** about 150–230 passengers per hour per lane at checkpoints; a common wait target of 10 minutes (from US TSA contexts; other countries may differ).

**Validation trick:** run a headless simulation of one pooled line with c servers and compare the average wait with an online Erlang C (M/M/c) calculator. Aim for within about 10–15%.

---

## 6. Level progression (each level teaches one concept)

1. One server: utilization, and why ρ near 100% makes waits explode.
2. Little's Law: estimate waits from queue length.
3. Shared vs separate lines: pooling.
4. Erlang C: minimum servers to hit a target.
5. Two stages: find and fix the bottleneck.
6. Mixed passengers: shortest job first and priority, fairness vs speed.
7. Peak waves: dynamic staffing rules.
8. Two checkpoints: routing and load balancing.

**Possible signature levels** from the classic queuing topics above (to pick later).

---

## 7. Style

- **Look:** low-poly, soft colors, cute miniature-diorama feel (a mix of Mini Metro clarity and Two Point Hospital's 3D people).
- **Clarity:** color only for meaning (green fine, yellow slowing, red problem). No tables during play, just small icons over lanes.
- **Motion ("modern feel"):** smooth movement. Passengers slide forward in line, barriers pop in with a small bounce, scores count up. Cheap with tweens.
- **Mood:** calm music, satisfying click sounds, emoji bubbles over passengers (😊 → 😐 → 😡).
- **Juice, but with limits:** keep effects subtle so they don't hurt readability.
- **Camera:** rig-based RTS style (pan with WASD/edge/middle-drag, rotate with Q/E, zoom with the wheel), with a slightly low field of view for a diorama look.
- **Lighting:** URP, soft directional light, ambient occlusion, light bloom, color grading.

**Reference games:** Mini Metro (readability, short sessions), Mini Motorways (weekly choose-1-of-2 upgrades, pausing), Cities: Skylines (overlays must show the cause of a jam), Airport CEO (checkpoint sizes, visible wait times), Two Point Hospital/Campus (star goals, funny agents), Overcrowd (start manual, automate later).

---

## 8. Technical plan (Unity)

**Engine decision: Unity 6 LTS with C#.**
- You already know the editor; Godot would cost about a month of relearning.
- Most management/queue references were made in Unity.
- Built-in NavMesh (AI Navigation package), a big asset store, free DOTween.
- Unity dropped its runtime fee in September 2024; Unity Personal stays free under its revenue/funding threshold (check current terms).
- Godot is a fine engine (lighter, free forever, friendlier for 2D and beginners), but there's no real advantage for this game.

**Architecture (most important):** separate the simulation from the visuals.
- `SimulationManager`: fixed tick (for example 10–20 Hz), plain C# classes (`Passenger`, `Queue`, `ServiceStation`, `Stage`, `ArrivalSchedule`), seeded random numbers so runs can be reproduced.
- `AgentView` MonoBehaviours: follow the model's position, with interpolation and tweens.
- Benefits: test the math headless, speed-up buttons (1×/2×/4×), and scale agents without physics.

**Queues as slots, not physics:** each queue is an ordered list of positions along a path (generated from the player's barriers; Splines package for curves). When someone is served, everyone moves up one slot.

**NavMesh only for free walking** (entrance → queue, reclaim → exit). Stagger `SetDestination` calls; never every frame. NavMeshObstacle with carving for player barriers.

**Agent state machine:** Arriving → WalkingToQueue → InQueue → BeingServed(stage n) → Walking → Exited, plus Balked and Reneged. Start with enum + switch; move to the State pattern only if needed.

**Performance:** `ObjectPool<T>` for agents; one manager loop instead of per-agent `Update()`; GPU instancing for materials; full Animator rigs only near the camera. Use the Profiler first. ECS/DOTS isn't justified below about 2,000 agents.

**Agent-count notes:** forum reports suggest NavMesh works well up to about 200–300 agents and stalls around 400–500 with frequent repathing. Measure on your own machine.

**Drag-and-click:** raycast from the mouse to the ground plane; snap to a 0.5 m grid; ghost preview (green valid / red blocked); commit on release, then rebuild queue slots.

**UI:** top bar (time, happiness, flights); small world-space labels above lanes (queue count, ≈ wait, ρ gauge); a heat overlay toggle; end-of-wave panel with simple charts. uGUI or UI Toolkit.

**Input:** new Input System (Pan, Rotate, Zoom, Select, DragStart/End).

**Tweening:** DOTween (free): eased slot moves (OutCubic), click punch-scale, OutBack pop-in for barriers, score counters, panel slides.

**Packages:** Unity 6 LTS, URP, AI Navigation, Cinemachine 3, Input System, DOTween, Splines (optional), Profiler.

**Not in version 1:** economy/building depth, multiple floors, narrative campaign, multiplayer, procedural maps, ECS/DOTS, mobile port, photorealistic art.

---

## 9. Art pipeline and 3D tools

**Tools:** Blender (models), Unity (logic and scene), Mixamo (free auto-rig and animations for passengers).

**Phase 1: use free packs first.** Kenney (CC0) and Quaternius (free for personal and commercial use; check each pack's license). Pick one pack and one palette. Build your own only when no pack has what you need.

**Blender pipeline:** model in Blender → export FBX → Unity (import, prefab, add `AgentView`) → Mixamo for character animation.

**Blender essentials (learn now):**
- Navigation, select/move/rotate/scale (G/R/S + axis), edit mode (vertex/edge/face)
- Extrude (E), inset (I), loop cut (Ctrl+R), bevel (Ctrl+B)
- Mirror modifier (for symmetric cars and people)
- Flat-color materials or one shared palette texture
- Apply transforms (Ctrl+A), pivot at the base, 1 unit = 1 m
- Checking normals
- FBX export settings for Unity
- Mixamo upload/download

**Blender optional (only if needed):** UV unwrapping (for signs or logos), basic rigging (only if Mixamo fails), Array/Boolean modifiers, LODs.

**Skip for this project:** sculpting, hand keyframe animation of characters, Cycles rendering (Unity handles lighting), Geometry Nodes, simulations, texture painting, PBR texturing.

**First exercise:** a queue barrier post with a belt. Cylinder post, flattened base, stretched cube belt, two flat-color materials, pivot at the bottom center, real height about 1 m, transforms applied. Export FBX, import into Unity, check the size next to a capsule passenger.

**Learning approach:** learn by building, not by watching long videos. Ask "why" questions when stuck. Use AI as a reviewer (Blender connector can inspect the scene, normals, scale and pivot). Fail on purpose once (for example unapplied scale) to see the effect.

---

## 10. Version control

**Use GitHub** (free), with **GitHub Desktop** (no command line needed).

**Unity settings (Project Settings → Editor):**
- Asset Serialization: Force Text
- Version Control: Visible Meta Files
- Always commit `.meta` files

**Blender:** `.blend` files are binary, so use **Git LFS** for `.blend`, `.fbx`, images, audio.

**Repo layout:**
```
throughput/
├── Unity/        (Unity project)
├── Blender/      (.blend files, LFS)
├── docs/         (design notes, devlogs)
├── .gitignore    (Unity template)
└── .gitattributes (LFS rules)
```

**Workflow:** edit in Blender → save → export FBX into `Unity/Assets/Models/` → test in Unity → commit with a short message → push.

**Rules:** commit at the end of every session; tag each milestone (`v0.1-greybox`, etc.).

**Starter files:** a `throughput-repo.zip` with `.gitignore`, `.gitattributes`, README and folders was prepared. The user creates the repo on GitHub.

---

## 11. Roadmap (about 8 hours per week, roughly 40 weeks)

| Phase | Weeks | Goal | Exit criterion |
|---|---|---|---|
| 0. Setup | 1–2 | Install Unity 6 LTS, Git + LFS, URP; write one-page design doc; push repo | Repo and design doc exist |
| 1. Fundamentals | 3–8 | Unity basics, C#, prefabs, UI, raycasts. Mini-project A: click-to-spawn capsules that walk on a NavMesh with a counter | Can build small scenes without a tutorial |
| 2. Toy simulation | 9–13 | Queue math: λ, μ, c, Little's Law. Mini-project B: abstract queue sim with cubes, Poisson arrivals, validated against Erlang C | Within about 15% of theory |
| 3. Greybox | 14–21 | Input, rig camera, placement, state machine, NavMesh + slots. Checkpoint level with capsules, end-of-wave score | **Find-the-fun gate:** 3 testers, at least 2 want "one more" |
| 4. Vertical slice | 22–29 | DOTween, URP, audio, pooling, Profiler; swap in art; juice; 300 agents at 60 fps | One level feels final |
| 5. Content | 30–36 | 3–5 levels, 2 passenger types, 1-of-2 upgrades, priority lane, wait signs, stars saved | Full run from menu to final level |
| 6. Ship | 37–40 | Builds, settings, itch.io page, trailer GIF, postmortem | Version 1 on itch.io |
| 7. Optional | 41+ | Second scenario (street crossing or metro) on the same engine | Only if v1 is done |

**Rule:** if life gets in the way, cut levels, not phases. Expect real progress to be 20–30% slower.

---

## 12. Lightweight solo process

- **One-page design doc:** pitch, player fantasy, core loop, 4–5 verbs, win/lose rules, MVP list, "NOT doing" list, session length.
- **Board:** Ideas → This 2 Weeks → Doing → Done. Ideas stay put until the current milestone is fun.
- **Two-week cycles:** end each with a playable build and a short devlog (what changed, what's fun, what's broken, next task, hours spent).
- **Playtesting:** watch silently; ask "what was the problem, and what did you try?"
- **Session rhythm (2-hour block):** 10 min reread devlog, 90 min on one task, 10 min commit and write the next task.
- **Ship-necessary razor:** before adding anything, ask "can we ship without it?"

---

## 13. Learning resources (filtered)

- Unity Learn: Essentials and Junior Programmer (Create with Code 1). Use selectively with the "learn by building" approach.
- Game Maker's Toolkit: "The Unity Tutorial For Complete Beginners" (fast end-to-end view).
- Nystrom, *Game Programming Patterns* (free online): State, Object Pool, Update Method, Game Loop.
- Schell, *The Art of Game Design: A Book of Lenses* (3rd edition): review your design at milestones.
- Talks: "Mini Metro: When Less is More" (GDC 2017); "Expanding the Miniverse" (GDC 2023); "RimWorld: Contrarian, Ridiculous, and Impossible Game Design Methods" (GDC 2017); "Juice It or Lose It" (2012).
- David Maister, "The Psychology of Waiting Lines" (free PDF).
- Docs: Unity AI Navigation, Cinemachine 3, Input System, DOTween.

---

## 14. Common pitfalls to avoid

- Scope creep (whole airport + city + economy)
- Tutorial hell (apply every hour of learning to this project)
- Art before fun (keep capsules until the find-the-fun gate passes)
- Using physics or NavMesh for queues (use slots)
- Repathing every frame (stagger requests, pool agents)
- Premature DOTS or custom engine
- Unreadable screen (one overlay at a time)
- Realism over fun (tune for "aha" moments)
- No backups (Git + LFS, commit each session)
- Losing motivation (devlog "next task" notes, a playable build every two weeks)

---

## 15. Open items and next steps

1. Check "Throughput" by hand on the Steam store and itch.io.
2. Create the GitHub repo (you're doing this), add the Unity project, set Force Text and Visible Meta Files, commit, tag `v0.0-setup`.
3. Write the one-page design doc in `docs/`.
4. Blender: build the barrier post exercise, then the passenger (mirror modifier, then Mixamo).
5. Unity: Mini-project A (click-to-spawn with NavMesh).
6. Decide which queuing topics become the signature levels.
7. Draft the store-page description (once the name is confirmed).
8. Check the license of each art pack before using it.

---

## 16. Caveats

- Agent-count limits and throughput numbers come from forum reports and US-based sources; measure your own results.
- Engine comparisons are based on articles and community opinions, not official benchmarks.
- The pooled-queue advantage assumes identical servers and no strategic behaviour. Real systems may differ.
- Art licenses change; always check the current terms before publishing.
- Name checks were done with web search, which can miss small titles. Double-check Steam and itch.io directly.
