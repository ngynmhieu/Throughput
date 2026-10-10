# Visual Style

How Throughput looks and feels. Exact values (colours, sizes, type, components) live in the [Throughput Design System](https://claude.ai/artifact/Ux7qp2fzRbByeqNN4DHrMH). This file records the **decisions and direction** behind them, so they can be updated in one place.

Every choice here serves the [pillars](pillars.md): P1 every fix has a cost, P2 the cause is always visible, P3 fair, not lucky, P4 calm not frantic.

---

## Identity: two views

| View | Job | Feel |
|---|---|---|
| **Toy diorama** | The play view. You build in Plan and watch in Run. | Chunky toy pieces on a warm table |
| **Blueprint** | The map: a full-screen engineering overview of the whole level, in both Plan and Run | An engineer's drawing on navy paper |

- The blueprint opens with **one toggle (Tab, or the Map button)**, like opening a map in other games. It opens with a short fold-out (about 0.3 s).
- During a run, the simulation **keeps going** while the map is open. Pausing is a separate control.
- The blueprint is the main answer to P2: it always shows the whole level, with every problem marked.

## Camera

- **3D, fixed high angle like Overcooked**: looking down at about 55°, CCTV-style, with a narrow field of view for the miniature look.
- **Pan:** hold the left mouse button and drag (also WASD/arrows). **No edge scrolling:** moving the mouse to the screen edge does nothing. **Zoom:** scroll wheel. **No rotation.**
- In Plan, left-drag on the floor pans the camera. When a tool is selected, a left click places it. The exact split between "drag to pan" and "click to place" will be settled when building the input system.
- Levels can be bigger than one screen. Off-screen problems are covered by the blueprint map and by edge alerts.
- Walls stay low (max 1.2 m), so the camera never loses the queue.

## Colour

- Diorama: warm cream table and floor; **belt blue** accent (Run button, selection, queue belts).
- Blueprint: navy paper with pale cyan lines.
- **Green / yellow / red are reserved** for fine / slowing / problem, always paired with a shape (● ▲ ■). Passengers and objects never use them.
- Decided 2026-10-10: the accent stays blue in both views. A warm green accent was considered and dropped, because green already means "fine".

## Characters

**Direction: a mix of "chunky toy" (A) and "peg doll" (B).** Both were tried in the look test. Neither is final, and the details will be refined when the real models are built in Blender.

| | A · Chunky toy (Overcooked-like) | B · Peg doll (wooden toy) |
|---|---|---|
| Head | Big and round (about 1/3 of height) | Smaller ball head |
| Body | Bean-shaped, soft | Smooth turned peg, painted base ring |
| Limbs | Floating hands, small feet, no arms | None |
| Face | Dot eyes | Dot eyes |
| Movement | Bouncy walk, hands swing | Slides with a side-to-side wobble |
| Feel | More personality | More "toy on a table", cheapest to build (no rigging) |

**Possible mix to explore later:** B's peg body and painted finish with A's bigger head and floating hands.

**Rules that apply whatever the final style:**
- **Read from above.** The camera mostly sees heads and shoulders, so passenger types differ by **what's on top**. Colour is only the second cue.

  | Type | From above | Body colour |
  |---|---|---|
  | Staff | Cap with brim | Navy uniform |
  | Business | Flat-top hair, briefcase | Charcoal |
  | First-timer | Wide-brim hat, big teal backpack | Cream |
  | Family | Adult + smaller child + stroller, move as one group | Plum |

- Chunky proportions, rounded bevels, matte flat colours, no textures.
- No rigging or walk-cycle animation is needed for either style. Movement is done with bob, wobble and hand swing in code.
- Never use green, yellow or red for clothes.

**Not liked yet:** the look-test figures are built from basic code shapes and lack detail and charm. They're shape and proportion sketches only.

## UI and overlays

- Labels float over the world: lane labels with busy gauges, "≈ x min" wait signs, and mood bubbles (😊 😐 😡) that appear only when a passenger's mood changes.
- Fonts: Nunito (rounded) for reading, IBM Plex Mono for numbers that change.
- Screen layout: level and goal top-left, Map top-right, tool tray bottom-centre (Plan) or the manager-action bar + pause (Run), Run button bottom-right.
- Runs play at **1× only** with a pause (D4 revised). After a run, a scrubbable replay.
- Full component list and rules: see the design system.

## Motion and mood

- Soft motion: passengers slide forward in line, placed objects pop in with a small bounce, result numbers count up.
- No screen shake, flashing or alarm sounds, even when things go wrong (P4).
- Lighting: one soft sun from the upper left, soft shadows, warm ambient light, identical in every level.

## Look test

- [prototypes/look-test.html](prototypes/look-test.html) is a **throwaway** 3D scene (Three.js) used to judge camera, colours, readability and character direction. It isn't game code. Press C to switch character style and Tab for the map.
- Result (2026-10-10): camera, layout and overall look approved. Characters approved as a direction (A + B) but need much better detail when built for real.

## Open items

| Item | When |
|---|---|
| Final character style (A, B or a mix) and detail level | When modelling characters in Blender |
| Add character rules to the design system | After the final character style |
| Real icon set (tool icons are placeholders) | When building the UI |
| Logo | Later, optional |
| Sound and music guidelines | Later |

## Change log

| Date | Change |
|---|---|
| 2026-10-10 | First version: two views, camera, colour, characters (A + B direction), UI, motion, look test result. |
| 2026-10-10 | Camera pan changed to hold left mouse + drag; edge scrolling removed. |
| 2026-10-10 | Runs are 1× only with pause; manager actions during runs (D4 revised). Design system RunButton updated. |
