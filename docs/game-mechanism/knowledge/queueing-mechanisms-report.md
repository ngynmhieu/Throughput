# Queueing Mechanisms: Research Report

Research date: 2026-10-10. Purpose: collect queueing concepts that could drive Throughput's simulation, judge each one as a game mechanic, and set up the next design discussion.

**Context from the brainstorm so far:**
- Main subjects: queueing theory and game design.
- The theory is the hidden engine. Players are never forced to learn it.
- The game style is **Plan → Watch**: design the system, run it, read the result, edit, run again.

Each entry below gives the concept, the "aha" a player could feel, how it could work as a mechanic, and a fit rating for Plan → Watch:
- ★★★ = core
- ★★ = good for a level or a twist
- ★ = probably skip

---

## A. Foundations: the engine every level runs on

### A1. Arrival processes
- **Concept:** Customers arrive at a rate λ. In theory, arrivals are often *Poisson*: random and memoryless. Real airports have *non-homogeneous* arrivals, meaning waves tied to flight departures.
- **Mechanic:** a flight board defines the arrival waves. The player can read the board before the run, which is good for planning.
- **Aha:** "The average load was fine, but the 7:40 wave broke everything."
- **Fit:** ★★★. This is the "problem statement" of every level.

### A2. Service-time variability (coefficient of variation)
- **Concept:** Two lanes with the same *average* speed perform very differently if one is erratic. Variability is measured by the coefficient of variation (CV = standard deviation / mean).
- **Mechanic:** passenger types (business traveller = fast and steady, family with liquids = slow and erratic). A "standardize" option, such as bin pre-loading, reduces CV but not the mean.
- **Aha:** "I didn't make anyone faster, but waits halved."
- **Fit:** ★★★

### A3. Utilization and the "hockey stick"
- **Concept:** utilization ρ = λ / (c·μ). Wait time grows roughly like ρ / (1 − ρ). Going from 70% to 85% is mild, but 85% to 95% explodes. Above 100%, the queue grows forever.
- **Mechanic:** a ρ gauge per stage with green, yellow and red bands. The cheapest-looking design (all staff at 98%) fails.
- **Aha:** "One more officer cut the wait from 25 minutes to 6."
- **Fit:** ★★★. The single most important "feel" of the game.

### A4. Kingman's formula (VUT equation)
- **Concept:** the approximate wait at one server ≈ **V**ariability × **U**tilization × service **T**ime. The terms *multiply*, so high variability is worst exactly when utilization is high.
- **Use:** not a player-facing mechanic. It's the **design tool for you** to tune levels and the formula for the "Expected wait ≈ X min" preview.
- **Aha (for you):** reducing variability is a substitute for adding staff, but only near saturation.
- **Fit:** ★★★ as a hidden tool.

### A5. Little's Law (L = λ·W)
- **Concept:** average number in the system = arrival rate × average time in the system. It holds for almost any queue.
- **Mechanic:** wait-time signs show "≈ W min" computed as queue length ÷ recent throughput. Players can read live waits from queue length.
- **Fit:** ★★. Mostly for display. It's useful for the signs and the post-run panel.

### A6. Erlang C and square-root staffing
- **Concept:** Erlang C (the M/M/c model) gives the probability of waiting and the average wait for *c* parallel servers. The **square-root staffing rule** says staff needed ≈ load + β·√load. Twice the passengers does *not* need twice the staff.
- **Mechanic:** budgets that look too tight for big levels are actually enough, which rewards trusting economies of scale.
- **Aha:** "Double the passengers, but only 1.5× the staff."
- **Fit:** ★★. It's also your **validation test** for the simulation.

### A7. Erlang A (abandonment)
- **Concept:** Erlang C assumes nobody leaves. In real systems with abandonment, Erlang C is pessimistic, and Erlang A, which models impatient customers, matches better. One airline call-centre study found exactly this.
- **Use:** if you add reneging (E2), validate against Erlang A rather than Erlang C.
- **Fit:** ★★ as a hidden tool.

---

## B. Structure: how the system is laid out

### B1. Pooling (one shared line vs separate lines)
- **Concept:** one serpentine line feeding *c* agents beats *c* separate lines. No agent sits idle while someone waits in another line.
- **Mechanic:** barrier layout decides it directly.
- **Aha:** "Same staff, same passengers, half the wait."
- **Fit:** ★★★

### B2. The counter-example: dedicated lines with ownership
- **Concept:** an emergency department that switched from pooled to dedicated queues saw about 17% shorter length of stay and about 9% shorter waits. Staff felt *ownership* of "their" patients and managed flow more actively (Song, Tucker & Murrell, *Management Science*, 2015).
- **Mechanic:** a late-game staff trait or upgrade, "team ownership", that speeds up dedicated lanes. It breaks the rule from B1 on purpose.
- **Fit:** ★★ as a late twist. Keep the original brainstorm note.

### B3. Tandem stages and the bottleneck
- **Concept:** in a line of stages (ID check → scanner → bag search), throughput = the slowest stage. Adding capacity anywhere else changes nothing.
- **Mechanic:** a multi-stage checkpoint where a highlight shows the slowest stage.
- **Aha:** "I added three officers at ID check and nothing happened."
- **Fit:** ★★★

### B4. Finite buffers, blocking and spillback
- **Concept:** if the space between stages is limited, a full downstream stage **blocks** the upstream one. Upstream servers stand idle even though they have customers. Textbook formulas mostly break here, so simulation is needed.
- **Mechanic:** limited floor space between stages. If the scanner area overflows, the ID desks freeze. Space becomes a real resource, which ties into the "barriers take space" principle.
- **Aha:** "The bottleneck was downstream, but the symptom showed up upstream."
- **Fit:** ★★★. Very visual, and only a simulation can show it, which makes it a strong argument for the game existing at all.

### B5. The shifting bottleneck
- **Concept:** fix the bottleneck and a new one appears somewhere else (Theory of Constraints).
- **Mechanic:** levels designed so the first obvious fix exposes a second bottleneck. That suits Plan → Watch → edit → watch again.
- **Fit:** ★★★

---

## C. Discipline: who gets served next

### C1. FIFO vs shortest job first
- **Concept:** serving short jobs first (shortest job first, SJF, or its preemptive form SRPT) minimises *average* wait. FIFO is fair but slower on average.
- **Mechanic:** an "express lane: ≤1 bag" rule is a practical form of SJF.
- **Fit:** ★★★

### C2. The convoy effect (head-of-line blocking)
- **Concept:** under FIFO, one very slow customer at the front holds up many quick ones behind them. Average wait inflates mostly because of the rare slow jobs.
- **Mechanic:** the "family with 4 strollers" passenger who freezes a whole lane. It's the visible reason express lanes exist.
- **Aha:** "One person caused that whole jam."
- **Fit:** ★★★. It's easy to *see*, which is perfect for the Watch phase.

### C3. Priority classes and fairness
- **Concept:** priority for one class (crew, short-connection passengers) speeds them up and slows everyone else. Total work stays the same; it just gets redistributed.
- **Mechanic:** a priority lane toggle. The score tracks the *worst-off* group, not only the average.
- **Fit:** ★★★. It matches "every fix has a cost."

### C4. Preemption
- **Concept:** interrupting a service in progress to serve a higher-priority customer.
- **Fit:** ★. It's odd at a security desk. Skip it.

---

## D. Routing: where each arrival goes

### D1. Random vs join-shortest-queue (JSQ)
- **Concept:** sending people to a random lane is bad. Sending them to the shortest line is much better. Sending them to the line with the *least work* (queue × service time) is better still when passengers vary.
- **Mechanic:** a routing rule at the entrance, chosen by the player.
- **Fit:** ★★★

### D2. The "power of two choices"
- **Concept:** if each arrival checks only **2 random lanes** and joins the shorter, you get almost all the benefit of checking *every* lane. The long-queue tail shrinks dramatically (double-exponential instead of geometric decay; Mitzenmacher; Luczak & McDiarmid 2006).
- **Mechanic:** a cheap staffing option, like a "guide who looks at 2 lanes", versus an expensive "full display board" (full JSQ). Players discover that the cheap one is nearly as good.
- **Aha:** "Half the price, 90% of the effect."
- **Fit:** ★★. It's a great surprise, but subtle to show.

### D3. Selfish routing: Braess and Downs–Thomson paradoxes
- **Concept:** when *passengers choose for themselves*, adding capacity can make everyone slower. Each person picks their own fastest option, and the equilibrium is worse than a coordinated plan (Cohen & Kelly 1990 for queueing networks; the Downs–Thomson effect for parallel queues). Giving people better information about queue states reduces this.
- **Mechanic:** a two-checkpoint level (level 8) where passengers pick the checkpoint themselves. Opening a new fast-track *shortcut* makes overall waits worse. The fix is signage and information, or removing the shortcut.
- **Aha:** "I *closed* a lane and everything got faster."
- **Fit:** ★★★ as a signature level. This is the kind of counterintuitive moment the game is built for.

### D4. Jockeying (lane switching)
- **Concept:** customers switch to another line if it looks shorter.
- **Mechanic:** passenger behaviour visible in Watch. It mostly works as automatic, imperfect JSQ.
- **Fit:** ★★. It adds life to the visuals and complicates the math. Maybe later.

---

## E. Customer behaviour

### E1. Balking
- **Concept:** a customer sees a long line and never joins (diverts or complains).
- **Mechanic:** the threshold depends on the visible queue length. Wait-time signs can *raise* or *lower* balking.
- **Fit:** ★★

### E2. Reneging (giving up mid-wait)
- **Concept:** a customer leaves after their patience runs out. At an airport, patience depends on the time left to the flight.
- **Mechanic:** a patience bar. Running out means a missed flight, which is a failure metric. A "short-connection" priority rule interacts with this.
- **Fit:** ★★★. It gives the fail condition real meaning.

---

## F. Control policies (written in Plan, executed in Watch)

### F1. Threshold policies with hysteresis
- **Concept:** "open lane when queue > 20, close when queue < 5". The gap between the two thresholds stops lanes flickering open and shut.
- **Mechanic:** per-lane open/close thresholds. This is the core of the rule system.
- **Aha:** "Opening at 20 and closing at 19 made staff run back and forth all day."
- **Fit:** ★★★

### F2. Setup and switchover time
- **Concept:** opening a lane isn't instant. Staff must walk over and set up (say 2 minutes). Thresholds must *anticipate* demand.
- **Mechanic:** a visible "opening…" timer. It makes F1 a real puzzle instead of trivially "open at 1".
- **Fit:** ★★★. A small rule that makes planning matter.

### F3. Server vacations and breaks
- **Concept:** servers leave periodically (breaks, shift changes).
- **Mechanic:** planning *when* staff take breaks around flight waves.
- **Fit:** ★★. A good level twist.

---

## G. Shaping demand

### G1. Virtual queues and appointment slots (FastPass-style)
- **Concept:** giving people a return time turns random arrivals into *scheduled* arrivals. That cuts variability (see A2). The documented side effect: Disney's standby lines slowed sharply and some guests waited longer overall. Virtual-queue holders weren't "in" the physical line but still consumed capacity.
- **Mechanic:** a "booked security slot" option for a share of passengers. It smooths arrivals but starves or punishes the standby line.
- **Aha:** "Booking helped the bookers and hurt everyone else."
- **Fit:** ★★. A strong late-game trade-off.

---

## H. Perceived wait vs actual wait (Maister's psychology)

David Maister's *The Psychology of Waiting Lines* (1985) argues that how a wait *feels* matters as much as its length. The commonly cited propositions:

1. Unoccupied time feels longer than occupied time.
2. Waits before the service starts feel longer than waits inside it.
3. Anxiety makes waits feel longer.
4. Uncertain waits feel longer than known waits.
5. Unexplained waits feel longer than explained ones.
6. Unfair waits feel longer than fair ones.
7. People wait longer for more valuable service.
8. Waiting alone feels longer than waiting in a group.

**Mechanic idea:** passenger happiness depends on *perceived* wait, not only actual wait. Perceived wait = actual wait × modifiers:
- **Wait-time signs** remove uncertainty (#4).
- **Screens and entertainment** occupy time (#1).
- **Visible fairness:** someone overtaking them in a priority lane *hurts* happiness (#6).
- **Bin pre-loading zones** move part of the wait "inside the process" (#2).

**Why this matters:** it creates a **second axis of optimisation**. Some levels can be won without making the queue faster, only by making it *feel* better. It's cheap to simulate, since it's just a multiplier, and the trade-offs are easy to read.

**Fit:** ★★★. It's the clearest way to add design depth without adding math.

---

## Patterns from similar games

- **Flowline**, a throughput game based on queueing and Theory of Constraints, appeared in search as a Steam listing (app 5071610). It features finding the constraint, watching it *shift*, buffers for variability, and constraints that can sit in the road *between* factories. Its developer says the simulation is tested against Hopp & Spearman's textbook bounds on every code change. **This is the closest comparable game found.** It's worth studying, and its approach (tests against theory bounds) is exactly what was planned for Throughput. I couldn't load the Steam page itself, so please verify by hand. Note: "Flowline" was already a rejected name for this project.
- **shapez 2:** the key skill is *planning* throughput before building, then untangling bottlenecks. This confirms that Plan → Watch works for flow puzzles.
- The common lesson: these games make the bottleneck **visible** and **move it around**. Adding raw capacity is never enough.

---

## Shortlist for Throughput v1

**Core engine (needed in every level):**
A1 arrival waves · A2 variability · A3 utilization · B1 pooling · B3 bottleneck · C1/C2 discipline and convoy effect · E2 reneging · F1 + F2 thresholds with setup time.

**Signature "aha" levels (pick a few):**
- B4 blocking and spillback
- C3 priority vs fairness
- D3 Braess: closing a lane helps
- H perceived-wait level

**Late twists (after v1 works):**
B2 ownership · D2 power of two choices · F3 breaks · G1 booked slots.

**Hidden tools for you (not player-facing):**
Kingman for the wait preview and level tuning · Erlang C and Erlang A for validating the simulation.

---

## Questions to discuss next

1. **Which of the four signature ideas excite you most?** That choice shapes the level list more than anything else.
2. **Does H (perceived wait) become a second axis?** That would mean happiness ≠ speed. It adds depth, but also a second number players must track.
3. **Who chooses lanes, you or the passengers?** D3 only works if passengers route themselves. The current design has the *player* set routing rules. Both can coexist (player rules first, selfish passengers in late levels), but this needs deciding.
4. **How far into networks do we go?** B4 (finite space between stages) needs the simulation to track space and blocking from the start. Adding it later means rewriting the core.
5. **Does all this confirm discrete-event simulation?** (This is still the open question from our debate.) Most items above are naturally *events*: arrival, service ends, patience runs out, lane finishes opening. Thresholds (F1) are the only part that prefers ticks.

---

## Sources

- [Kingman's formula (Wikipedia)](https://en.wikipedia.org/wiki/Kingman%27s_formula)
- [The Kingman Formula: Variation, Utilization, and Lead Time (AllAboutLean)](https://www.allaboutlean.com/kingman-formula/)
- [Kingman formula queue (Fabrico)](https://www.fabrico.io/blog/kingman-formula-queue/)
- [Counterintuitive queueing examples (Texas A&M, Gautam)](https://gautam.engr.tamu.edu/paradox.pdf)
- [Mitzenmacher, power of two choices (Harvard)](https://www.eecs.harvard.edu/~michaelm/NEWWORK/postscripts/onrandlb.pdf)
- [Luczak & McDiarmid, supermarket model (Project Euclid)](https://www.projecteuclid.org/download/pdfview_1/euclid.aop/1147179980)
- [Scalable Load Balancing in Networked Systems (survey)](https://ar5iv.labs.arxiv.org/html/1712.08555)
- [Maister, The Psychology of Waiting Lines](https://davidmaister.com/blog/201/The-Psychology-of-Waiting-Lines.html)
- [Don Norman on the psychology of waiting lines](https://jnd.org/the-psychology-of-waiting-lines/)
- [Simulating Disney's virtual line system (Towards Data Science)](https://towardsdatascience.com/i-used-queuing-theory-to-simulate-disneys-new-virtual-line-system-bd593213c006/)
- [History of queueing at Disney parks (Parklore)](https://parklore.com/vault/waiting-game/3)
- [The myth of more staff (The Queue Report)](https://thequeuereport.substack.com/p/the-myth-of-more-staff-why-throwing)
- [Erlang C vs Erlang A in an airline call centre](https://www.researchgate.net/profile/Kaushik-Nag-3/publication/323137574_Evaluating_erlang_C_and_erlang_A_models_for_staff_optimization_A_case_study_in_an_airline_call_center/links/6388552bfee13e4fe53019bc/Evaluating-erlang-C-and-erlang-A-models-for-staff-optimization-A-case-study-in-an-airline-call-center.pdf)
- [Airport security screening and queueing (Regattieri et al.)](https://iris.unimo.it/handle/11380/633598)
- [Braess's paradox (Wikipedia)](https://en.wikipedia.org/wiki/Braess%27s_paradox)
- [Cohen & Kelly 1990, A Paradox of Congestion in a Queueing Network](https://resolve-he.cambridge.org/core/journals/journal-of-applied-probability/article/abs/paradox-of-congestion-in-a-queuing-network/C3B6ACE2F6B0367063EDCB68BBE3B44C)
- [Braess's paradox with state-dependent routing (Cambridge)](https://www.cambridge.org/core/journals/journal-of-applied-probability/article/braesss-paradox-in-a-queueing-network-with-statedependent-routing/A5C236563259B22CB433B8F851126AD5)
- [Downs–Thomson effect in a Markov process (Cambridge)](https://resolve.cambridge.org/core/journals/probability-in-the-engineering-and-informational-sciences/article/downsthomson-effect-in-a-markov-process/72E25F8D4739F1297A62082DD47258C6)
- [Queueing network models, MIT 1.041 lecture](https://web.mit.edu/1.041/spring2023/lectures/L10-queuing-network-models-2023sp.pdf)
- [The Convoy Effect](https://third-bit.com/2026/06/05/convoy-effect/)
- [FCFS, SJF and SRTF (AlgoMaster)](https://algomaster.io/learn/operating-systems/fcfs-sjf-and-srtf)
- [Song, Tucker & Murrell, The Diseconomies of Queue Pooling (summary)](https://academicnewsletter.sufe.edu.cn/info/393552)
- [Shorter ER wait times through smarter queues (Wharton)](https://magazine.wharton.upenn.edu/issues/spring-summer-2017/shorter-er-wait-times-through-smarter-queues)
- [Flowline on Steam](https://store.steampowered.com/app/5071610)
- [shapez 2 guide (GameBrief)](https://www.gamebrief.net/blog/shapez-2-complete-guide-hub)

**Caveats:** several findings come from abstracts and secondary summaries, not full papers (Regattieri, Cohen & Kelly, Maister's list wording, the Flowline description). Check the originals before quoting exact numbers.
