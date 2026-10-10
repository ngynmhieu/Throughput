# Game Mechanism: Scopes

Every queueing mechanism plays one role in the game. This file sorts them by role, so it's clear who controls what. Concept numbers like [7] refer to [knowledge/queueing-mechanisms.html](knowledge/queueing-mechanisms.html).

Some mechanisms appear in two scopes. Variability, for example, is a *difficulty* (unpredictable passengers) and also the target of a *tool* (standardizing).

---

## 1. Passenger (NPC) behaviour: what passengers do on their own

The player can't control these. Per decision D6, passengers are obedient by default, and behaviours marked *unlockable* are switched on per level.

| Behaviour | What the passenger does | When |
|---|---|---|
| Arriving in waves [1] | Arrives according to flight times, in clumps | Always |
| Passenger types [2] | Business (fast, steady), family (slow, unpredictable), first-timer (confused) | Always |
| Reneging [17] | Waits, loses patience, leaves, which means a missed flight | Always |
| Feeling the wait [22] | Happiness follows how long the wait *feels* | Unlockable (mid-game) |
| Lane switching [15] | Jumps to a line that looks shorter | Unlockable |
| Balking [16] | Sees a long line and refuses to join | Unlockable |
| Choosing own route [14] | Picks what looks fastest for *them* | Unlockable (late) |
| Groups (new) | Families move and are served as one unit | Unlockable |
| Rework (new) | Fails a scan and goes back for a re-scan | Unlockable |

## 2. Player tools: what the player adds to improve the queue

The toolbox, used during Plan. Early levels start from one plain line, and the player adds these.

| Category | Tool | Based on |
|---|---|---|
| Layout | Shared serpentine line vs separate lines | Pooling [4] |
| | Size of waiting areas between stages | Blocking [7] |
| Staffing | Officers per stage | Utilization [3] |
| | Moving staff to the slowest stage | Bottleneck [6] |
| | Open/close rules with a gap | Hysteresis [18] |
| | Break schedule | Breaks [20] |
| Sorting | Express lane (≤1 bag) | Shortest job first [10] |
| | Priority lane (tight connections) | Priority [11] |
| Routing / information | Entrance rule: random / shortest / least work | Routing [12] |
| | Guide officer (checks 2 lanes) vs display board (all lanes) | Power of two [13] |
| | Signs showing real waits at each checkpoint | Counters Braess [14] |
| Standardizing | Bin pre-load area, "liquids out" instructions | Lower variability [2] |
| Demand | Booked time slots for X% of passengers | Booked slots [21] |
| Comfort | Wait-time signs, screens, hidden priority lanes | Perceived wait [22] |
| Late twist | "Team ownership" trait for dedicated lanes | Ownership [5] |

## 3. Difficulty: what each level throws at the player

The pressures a level designer turns up.

| Pressure | Why it's hard |
|---|---|
| Bigger or closer flight waves [1] | Peaks exceed capacity even when the average doesn't |
| More unpredictable passengers [2] | Same staff, longer queues |
| Tight staff budget [3] | Staff run near 100% busy, where waits explode |
| Multi-stage checkpoints [6] | The player must find the real bottleneck |
| Small floor space [7] | Blocking freezes earlier stages |
| Hidden second bottleneck [8] | The first fix reveals the next problem |
| Slow passengers mixed in [9] | Convoy jams |
| Tight connections [17] | Low patience, so mistakes become missed flights |
| Setup time [19] | Rules must fire early |
| Selfish passengers [14] | Adding capacity can make things worse |
| Equipment breakdown (new) | A scanner stops for minutes mid-wave |
| Locked elements (D5) | In "broken" levels, some parts can't be moved |
| Mixed goals (D9) | Main goal plus extras: wait, fairness, budget |

## 4. Feedback: how the player understands what happened

In Plan → Watch, the player can only learn by reading the result, so this scope matters a lot.

| Readout | Based on |
|---|---|
| Busy gauge per stage (green/yellow/red) | Utilization [3] |
| Slowest stage glows red after a run | Bottleneck [6] |
| "≈ 6 min" signs above lanes | Little's Law [23] |
| "Expected wait ≈ X min" preview before Run | Kingman [24] |
| Results: main goal, extras, worst-off group, happiness | D9, Priority [11], Perceived wait [22] |

## 5. Hidden math: for the developer only

| Tool | Use |
|---|---|
| Erlang C / Erlang A [25] | Validate the simulation (target: within ~10–15%) |
| Kingman's formula [24] | Wait preview; tune levels without full runs |
| Little's Law [23] | Wait-time signs; sanity checks |

## 6. Costs: the price of every tool

The design principle "every fix has a cost":

| Tool | Cost |
|---|---|
| Staff | Budget |
| Barriers, waiting areas | Floor space |
| Express or priority lanes | Slower for everyone else |
| Display board | Money |
| Booked slots | Standby line suffers |
| Tight open/close rules | Staff walking back and forth (setup time wasted) |

---

## The pairing rule: every difficulty needs a matching tool

A good level gives a problem from scope 3 that a tool from scope 2 can solve.

| Difficulty | Tool the player can find |
|---|---|
| Flight waves | Open/close rules + setup time |
| Convoy jams | Express lane |
| Bottleneck | Move staff to the slowest stage |
| Blocking | Bigger waiting area, or speed up downstream |
| Unpredictable passengers | Standardize (bin pre-load) |
| Selfish routing | Information signs, or close the shortcut |
| Low patience | Priority lane for tight connections |
| Uncertain, unfair waits (perceived) | Wait-time signs, screens, hidden priority |
| Equipment breakdown | Spare capacity, open/close rules |

- A difficulty with **no matching tool** feels unfair.
- A tool with **no matching difficulty** is decoration.
