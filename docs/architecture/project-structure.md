# Architecture: Project Structure

How the repo is laid out across all phases: Sim, Unity and Blender. Decided 2026-10-10.

## Decision A5: the engine lives inside the Unity project from the start

- **Choice:** the simulation code lives in `Unity/Assets/Sim/` from phase 1, not in a separate .NET project.
- **Why:** the user wants to test inside the real project from day one, so nothing changes when phase 2 starts.
- **How "pure C#" is kept:** `Throughput.Sim.asmdef` has **No Engine References** turned on, so Unity itself refuses any `UnityEngine` use in the engine (A4).
- **Tests:** Unity Test Framework, **EditMode**. Run them from the Test Runner window, or headless:
  `Unity.exe -batchmode -projectPath Unity -runTests -testPlatform EditMode -testResults results.xml`
- **Rejected:** a standalone `.NET` project used as a local Unity package. It has faster tests, but phase 1 and phase 2 would then live in different places.

## Creating the project

- Let **Unity create it**; never make the folder by hand (it needs generated settings, the package manifest and `.meta` files).
- Editor: **Unity 6000.3.26f1** (Unity 6.3), template **Universal 3D (URP)**, location `Throughput/Unity`.
- Ways to do it: Unity Hub, `Unity.exe -batchmode -createProject <path> -quit`, or the Unity CLI.
- After creation, set *Project Settings → Editor*: **Asset Serialization = Force Text**, **Version Control = Visible Meta Files**.
- This is the **first build step** of phase 1's plan, done after the spec is approved.

## Repo layout

```
throughput/
├── AGENTS.md, CLAUDE.md            agent instructions
├── docs/                           design docs, specs, plans
├── Unity/                          the Unity project (created by Unity)
│   ├── Packages/manifest.json
│   ├── ProjectSettings/
│   └── Assets/
│       ├── Sim/                    PHASE 1: the queueing engine
│       │   ├── Throughput.Sim.asmdef      (No Engine References)
│       │   ├── Network/  Events/  Policies/  Behaviours/  Timeline/  Results/
│       │   └── Tests/
│       │       └── Throughput.Sim.Tests.asmdef   (EditMode)
│       ├── Game/                   PHASE 2+: Throughput.Game
│       │   ├── Scripts/            views, playback, input, plan editor
│       │   ├── Prefabs/            passengers, desks, scanners, barriers
│       │   ├── Levels/             level data (JSON), shared with the tests
│       │   ├── Scenes/
│       │   └── UI/                 HUD (UI Toolkit) from the design system
│       └── Art/Models/             FBX exported from Blender (PHASE 3)
├── Blender/                        PHASE 3: source art (.blend, Git LFS)
│   ├── characters/  props/
│   └── palette.png                 one shared colour texture (design system colours)
├── .gitignore                      Unity (Library/, Temp/, Logs/, obj/, Build/) + Blender backups
└── .gitattributes                  Git LFS: .blend, .fbx, .png, .wav, .mp3
```

## How the pieces connect

```
 Blender ──export FBX──► Unity/Assets/Art ──prefabs──┐
                                                     ▼
 Levels (JSON) ──► Sim (Assets/Sim) ──timeline──► Game views ──► the game
                    ▲
               EditMode tests (phase 1)
```

## Rules

- `Assets/Sim` never references `Assets/Game` or Unity. `Assets/Game` references `Assets/Sim`.
- Blender models: 1 unit = 1 m, pivot at the bottom centre, transforms applied, colours from the shared palette (design system).
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/` or `Build/`.
