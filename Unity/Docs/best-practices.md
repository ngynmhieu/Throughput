# Unity Best Practices

What Unity and the community recommend for organizing a project, and how Throughput follows it. Researched 2026-10-10.

Applies to the Unity project in `Unity/`.

**Important:** Unity has **no enforced standard** for folder layout. Its own guide says there is no single right way; the rule is *pick one, write it down, stay consistent*. This file is where we write ours down.

---

## 1. Folder names Unity treats specially

**What it is:** a few folder names change how Unity behaves. Everything else is just a name.

| Folder | What Unity does |
|---|---|
| `Editor/` | Code runs only in the editor, never in the built game |
| `Resources/` | Assets you can load by name at runtime (use sparingly) |
| `StreamingAssets/` | Files copied untouched into the build |
| `Plugins/` | Native or third-party DLLs |
| `Assets/ScriptTemplates/` | Replaces Unity's "new script" templates |

**In Throughput:** we don't use any of these yet. Level JSON files could go in `StreamingAssets/` later, or be loaded as `TextAsset`s.

---

## 2. Folder layout

**What Unity recommends:**
- Keep your own work in `Assets/`. Don't add extra root folders without a reason.
- Keep **your** assets apart from **third-party** ones (Asset Store, imported samples).
- Many guides suggest **one root folder named after the project** (for example, `Assets/_Project/`) so your work never mixes with imports.
- For small projects, group by **type** (Scripts, Prefabs, Materials). For bigger ones, group by **feature**, with Scripts, Prefabs and Tests inside each feature.
- Have a **sandbox** folder for experiments that never ship.
- Create only the folders you need.

**In Throughput:** we group by **layer**, which is a form of feature grouping:
```
Assets/
├── Core/    logic ("backend"): no Unity code; the engine is in Core/Sim/
├── Game/    presentation ("frontend")
└── Art/     models from Blender
```
The URP template's own folders (`Settings/`, `Scenes/`, `TutorialInfo/`) stay where they are.
**Possible later:** an `Assets/Sandbox/` folder for throwaway tests, ignored by the build.

---

## 3. Naming files and folders

**What Unity recommends:**
- **No spaces** in file or folder names. Unity's command-line tools have trouble with them. Use `PascalCase`: `SecurityLane.prefab`, not `security lane.prefab`.
- No special characters or Unicode.
- A **MonoBehaviour's file name must match its class name**: `LaneView.cs` contains `class LaneView`.
- If you change a naming rule, rename every affected file together.

**In Throughput:** `PascalCase` for every asset and folder. Folders directly in `Unity/` follow Unity's style too (`Assets/`, `Packages/`, `Docs/`). Markdown notes keep the repo's doc style (`best-practices.md`). Blender exports keep the same name as the `.blend` source (`Passenger.blend` → `Passenger.fbx`).

---

## 4. `.meta` files

**What it is:** every asset has a `.meta` file holding its **GUID**. Scenes and prefabs link to assets by GUID, not by name.

**What Unity recommends:**
- *Project Settings → Version Control → Mode* = **Visible Meta Files**.
- Always commit a `.meta` together with its asset.
- **Move and rename inside the Unity editor**, never in Windows Explorer, so the `.meta` file moves too.
- Never edit `.meta` files by hand, and never change GUIDs.
- Empty folders: git doesn't track them, but Unity makes a `.meta` for them, which causes mismatches. Add a `.keep` (or `.gitkeep`) file.

**In Throughput:** we already use `.gitkeep` in the empty scaffold folders.

---

## 5. Version control (git)

**What Unity and the community recommend:**

| Setting / rule | Why |
|---|---|
| *Project Settings → Editor → Asset Serialization* = **Force Text** | Scenes and prefabs become readable YAML, so git can diff and merge them |
| Commit `Assets/`, `Packages/`, `ProjectSettings/` | These are the project |
| Ignore `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `UserSettings/` | Unity regenerates them |
| Start from GitHub's Unity `.gitignore` | Covers the common cases |
| **Git LFS** for big binaries (models, textures, audio) | Keeps the repo small; binaries can't merge anyway |
| Everyone uses the **same Unity version** | Different versions rewrite assets and cause noise |
| Use **prefabs** and edit the prefab, not the scene | Prefab diffs are smaller and conflict less |

**In Throughput:** all of this is done (see `.gitignore`, `.gitattributes`, `ProjectVersion.txt` = 6000.3.26f1).
**Normal noise:** Unity sometimes rewrites settings files (such as `*_RPAsset.asset`) when it opens or upgrades. That's expected; just commit them.

---

## 6. Assembly definitions (`.asmdef`)

**What it is:** a `.asmdef` file makes its folder (and subfolders) compile into **its own DLL**, with an explicit list of what it may use.

**What Unity recommends:**
- Without asmdefs, all scripts compile into one big assembly, so **every change recompiles everything**. Asmdefs make compiles faster.
- Declare every dependency explicitly.
- Set a **Root Namespace** that matches the folder.
- Put **tests in their own assemblies** so they never ship in the game.
- Editor-only tools go in an assembly limited to the **Editor** platform.
- Unity's **package** layout is the closest thing to an official standard:
  ```
  Runtime/   Company.Package.asmdef
  Editor/    Company.Package.Editor.asmdef
  Tests/Editor/  Tests/Runtime/
  ```
- Asmdefs add setup work; use them where they pay off, not everywhere.

**In Throughput:**

| Assembly | Folder | Rule |
|---|---|---|
| `Throughput.Core` | `Assets/Core/` | **No Engine References**: Unity code is a compile error (A4) |
| `Throughput.Core.Tests` | `Assets/Core/Tests/` | Editor only, EditMode tests |
| `Throughput.Game` | `Assets/Game/Scripts/` | May use `Throughput.Core` and the Input System |

---

## 7. Namespaces

**What Unity recommends:** use namespaces to separate modules and avoid name clashes with third-party code. **Make folders match namespaces.**

**In Throughput:** `Throughput.Core.Sim.Network` lives in `Assets/Core/Sim/Network/`, and so on.

---

## 8. C# naming style

**What it is:** Unity publishes a free e-book, *Create a C# style guide*, based on Microsoft's C# conventions. Unity says no style is "right"; consistency is what matters.

| Thing | Style | Example |
|---|---|---|
| Classes, structs | PascalCase noun | `ServiceStation` |
| Interfaces | `I` + PascalCase | `IRoutingPolicy` |
| Methods | PascalCase verb | `ScheduleEvent()` |
| Methods returning `bool` | a question | `IsBlocked()`, `HasCapacity()` |
| Properties, public fields | PascalCase (prefer properties) | `QueueLength` |
| Private fields | camelCase with a prefix (Unity uses `m_`; `_` is also common) | `_queue` |
| Static fields | `s_` prefix (Unity's style) | `s_instance` |
| Constants | `k_` + PascalCase (Unity's style) | `k_SnapshotInterval` |
| Locals, parameters | camelCase | `arrivalTime` |
| Booleans | verb prefix | `isOpen`, `hasBreakdown` |
| Enums | PascalCase, singular | `LaneState.Open` |
| Events | verb phrase: `-ing` before, `-ed` after | `ServiceStarted` |
| Event-raising methods | `On` + event | `OnServiceStarted()` |

General rules:
- Readability over brevity; no abbreviations except loop counters and math (`rho`, `lambda` are fine in queueing code).
- No redundant names: `Lane.Length`, not `Lane.LaneLength`.
- One declaration per line; always write access modifiers (`private`).
- Use `var` only when the type is obvious.

**In Throughput (proposed):** follow the table, with `_camelCase` for private fields (shorter than `m_`, also standard in Microsoft C#). Settle this before phase 1 code starts.

---

## 9. Scenes and prefabs (phase 2+)

**What Unity recommends:**
- Split big levels into smaller scenes and load them **additively**.
- Build with **prefabs and nested prefabs**; edit the prefab rather than the scene.
- Use **Presets** (*Project Settings → Preset Manager*) for default settings on new assets.

**In Throughput:** one scene per screen (menu, level) is enough at first. Every lane object (desk, scanner, barrier) is a prefab.

---

## 10. VS Code setup

**What it is:** VS Code needs two extensions to work with Unity: **C# Dev Kit** (C# language support) and **Unity** (`visualstudiotoolsforunity.vstuc`: debugging, Unity warnings, autocomplete). Unity needs the `com.unity.ide.visualstudio` package (already installed) and *Preferences → External Tools → External Script Editor* = **Visual Studio Code**.

**What the Unity extension gives:**
- **Debugging:** F5 → *Attach to Unity*, set a breakpoint, run a test in the Test Runner, step through the code.
- **Unity warnings:** for example, a misspelled `Updat()` that Unity would never call.
- **Autocomplete and docs** for Unity methods (`Start`, `Update`…).
- It does **not** run tests from VS Code; tests still run from Unity's Test Runner.

**In Throughput (decided 2026-10-10):** work in **one VS Code window on the repo root**, not the `Unity/` window Unity opens.
- `.vscode/settings.json` sets `"dotnet.defaultSolution": "Unity/Unity.slnx"` so C# works from the root, and hides Unity's generated folders and `.meta` files.
- `.vscode/launch.json` has the *Attach to Unity* debug entry.
- Catch: double-clicking a script or error **inside Unity** still opens a second window on `Unity/`. Open files from VS Code instead (Ctrl+P). `Unity/.vscode/` is Unity's own config for that window; leave it alone.

---

## 11. Unity CLI (for AI agents)

**What it is:** Unity's command-line tool (`unity`). With the **Pipeline** package (`com.unity.pipeline`) in the project, it can drive the **open editor**: run C# (`unity command eval`), read the console, compile, run tests, edit scenes and assets. Unity recommends it over its old MCP server; agents that can run shell commands use it directly.

**In Throughput (set up 2026-10-10):**
- CLI installed at `%LOCALAPPDATA%\Unity\bin\unity.exe` (beta channel); Pipeline package added to `Packages/manifest.json`.
- Check the connection: `unity status` should show the project as **ready**.
- No MCP config: Claude runs `unity` commands directly (faster, fewer tokens).
- **Known Windows warning:** **Smart App Control** is on and blocks one unsigned file from the Pipeline package, `IlInterpreterAnalyzer.dll` (a compile-time code checker). Windows may show a ".NET blocked" notification after compiles. **It's harmless**: the project compiles and the CLI works (tested). Don't turn Smart App Control off for this; once off, Windows can't turn it back on without a reset.

---

## Throughput checklist

- [x] Visible Meta Files, Force Text serialization
- [x] `.gitignore` (Unity template) and Git LFS for binaries
- [x] Editor version pinned (6000.3.26f1)
- [x] Asmdefs: logic, tests, presentation
- [x] `.gitkeep` in empty folders
- [x] Rename `Sim` → `Core` (logic layer), engine in `Core/Sim/`
- [x] VS Code works from the repo root (section 10)
- [x] Unity CLI + Pipeline package (section 11)
- [ ] Set Unity's External Script Editor to Visual Studio Code
- [ ] Confirm the C# naming style (section 8)

## Sources

- [Unity: Best practices for organizing your Unity project](https://unity.com/how-to/organizing-your-project)
- [Unity: Naming and code style tips for C# scripting](https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity)
- [Unity e-book: Create a C# style guide](https://unity.com/resources/create-code-c-sharp-style-guide-e-book)
- [Unity Manual 6.0: Assembly definitions](https://docs.unity3d.com/6/Documentation/Manual/cus-asmdef.html)
- [Unity Manual: Package assembly layout](https://docs.unity3d.com/cn/2023.1/Manual/cus-asmdef.html)
- [Leiden University VR wiki: Better Practices](https://researchwiki.solo.universiteitleiden.nl/xwiki/wiki/researchwiki.solo.universiteitleiden.nl/view/VR%20in%20Unity3D/Better%20Practices/)
- [thoughtbot: How to Git with Unity](https://thoughtbot.com/blog/how-to-git-with-unity)
- [Diversion: Unity best practices](https://docs.diversion.dev/unity/unity-best-practices)
- [uhiyama-lab: Version control for Unity projects](https://uhiyama-lab.com/en/notes/unity/unity-version-control-git-lfs-team-workflow/)
