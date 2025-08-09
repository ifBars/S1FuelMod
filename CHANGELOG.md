# Changelog

> Dates are UTC. Entries note **branch**, **author**, **commit** (short SHA), and a short description. Trello items are listed where a commit obviously implements or completes a card/checklist.

---

## 2025‑08‑09

### Master branch (initialization and gameplay tuning)
- **master · SirTidez · 458c955** — Update README to match new project structure and scope (mirrors current feature set and setup).
- **master · SirTidez · d57262a** — Fix distance gating so refueling requires both the **player** and the **vehicle** to be near the pump (prevents accidental fills from far away). Maps to “Create proximity refueling logic.”
- **master · SirTidez · 2fae498** — Add config toggles to enable/disable **dynamic pricing** and **price‑by‑tier** behavior. Trello: “Dynamic Fuel Prices” checklist work.
- **master · SirTidez · 92f0bf5** — Set pump price to reflect the player’s **level/tier** and to **change by day**. Trello: “Set a fixed percentage… on every tier gained” & “Set a price based off of the current day…” marked complete.
- **master · SirTidez · ee1d2eb** — Append **price per liter** to the station hover text for immediate feedback. Trello: FuelStation “Implement cost calculation.”
- **master · SirTidez · f39538c** — Price of fuel is now **determined dynamically by day** (foundation of daily variance). Trello: Dynamic pricing card/checklist.
- **master · SirTidez · a1136be** — Increase **refuel rate** to feel more realistic during fills. (Pairs with showing the gauge while refueling below.)
- **master · SirTidez · aa3cfee** — Introduce a **VehicleType** field to simplify branching/switching logic for capacity/consumption. Aligns with Trello: “Implement per vehicle type fuel capacity.”
- **master · SirTidez · f73160d** — Lower default **fuel capacity** to **18 L** (baseline before per‑vehicle overrides).
- **master · SirTidez · 67bf6ce** — Add **conditional fuel capacity** per vehicle type (first pass). Trello: “Per vehicle type gas tank size” (completed on Aug 9) and checklist item done.
- **master · SirTidez · ce0e3a8** — Fix `onDestroy()` attempting to hide an already‑destroyed UI element (stability).
- **master · SirTidez · 6e4b193** — Make fuel capacity **configurable by vehicle type** (follow‑up refactor to prior capacity work).
- **master · SirTidez · e0029b8** — Drop some logs from **Info** to **Debug** (reduces noise).
- **master · SirTidez · e99a299** — Remove unused dependencies (project hygiene).
- **master · SirTidez · 9c6f0a6** — Show **FuelGaugeUI while refueling** (visual feedback). Trello: “put the fuel gauge back up during refueling” (completed Aug 9).
- **master · SirTidez · fdc80d0** — Fix `localPlayerIsDriver` check (prevents remote/observer edge cases from ticking).
- **master · SirTidez · 87f0bc3** — Sync master to match content from the main line (historical import).
- **master · SirTidez · 8c7781f** — Add project files (initial files on this branch).
- **master · SirTidez · 4352f34** — Add `.gitattributes` and `.gitignore`.

**Related Trello completions around this date**
- “Per vehicle type gas tank size” marked **Complete** (Aug 9, 06:36).
- “Start vehicles with a full tank if they have no saved fuel data” marked **Complete** (Aug 9, 05:08).
- “Put the fuel gauge back up during refueling” marked **Complete** (Aug 9, 03:41).
- Dynamic Fuel Prices checklist: tier increment and day‑adjusted pricing **Complete** (Aug 9, ~06:36).

---

## 2025‑08‑08

### main‑archived branch (foundational implementation & first pass features)
- **main‑archived · ifBars · 65289ef** — Change **fuel station constants** (tuning pass).
- **main‑archived · ifBars · 4a968dd** — General **bug fixes** (stability improvements).
- **main‑archived · ifBars · 460c0ce** — **Add fuel stations** in the world and wire‑up interaction (base locations + hooks). Trello: “FuelStation Component” & proximity/cost checklist.
- **main‑archived · ifBars · 3253f2a** — Remove `FuelPersistenceManager.cs` (moved to VehicleData‑backed persistence). Trello: “Extend VehicleData to contain fuel fields.”
- **main‑archived · ifBars · 4d99254** — Update README.md (document features and setup).
- **main‑archived · ifBars · e415c11** — Add **fuel persistence** for vehicles (save/load integration). Trello: “VehicleManager.GetSaveString patch” & “LandVehicle.Load / VehiclesLoader.Load.”
- **main‑archived · SirTidez · d4a8041** — **FuelGaugeUI tweaks** (UX improvements).
- **main‑archived · SirTidez · 4de3a53** — Optimization: Update loop only runs for the **current vehicle the local player is in** (prevents off‑vehicle ticking).
- **main‑archived · ifBars · 25118dc** — Update README.md (follow‑up docs).
- **main‑archived · ifBars · 04f4394** — **Initial commit: S1FuelMod v1.0.0** — base systems, Harmony patches, UI and persistence skeleton.

**Related Trello context prior to/around Aug 8**
- Core **Harmony patches** checklist completed for: `LandVehicle.ApplyThrottle` (consumption), save/load hooks, spawn attach, and UI show/hide on enter/exit. (Aug 8 around 20:08–20:10).
- **FuelStation Component** checklist all completed (MonoBehaviour, InteractableObject integration, proximity, cost). (Aug 8 21:00).
- **LandVehicleFuelSystem** checklist completed (component, consumption calc, UI updates, driver gating). (Aug 8 03:16–03:52).

---

## Contributors

- **SirTidez** — gameplay tuning, capacity logic, dynamic pricing controls, UI/UX polish, README updates, build/project setup on `master`.
- **ifBars** — initial architecture, stations, persistence, constants, early docs on `main-archived`.

---

## Trello ↔ GitHub highlights

- **Per vehicle type fuel capacity** — Implemented on `master` (67bf6ce / 6e4b193), Trello card **completed** Aug 9.
- **Fuel gauge visible during refueling** — Implemented on `master` (9c6f0a6), Trello card **completed** Aug 9.
- **Dynamic fuel prices** (daily variance + tier scaling) — Implemented across `master` commits (f39538c, 92f0bf5, 2fae498, ee1d2eb), aligns with Trello checklist items **completed** Aug 9.
- **FuelStation gameplay** (proximity/cost) — Implemented in `main‑archived` (460c0ce) and refined in `master` (d57262a, ee1d2eb). Trello FuelStation checklist **complete**.

---

## Unreleased / In‑progress (from Trello)

- **Balance fuel consumption by vehicle type** (checklist “Implement per vehicle type fuel consumption” still **incomplete**).
- **Dynamic Fuel Prices** — two checklist items **incomplete**: determine **base price** and **daily variance** targets. (Feature works, but balancing values are TBD.)
- **Multiplayer syncing** — noted not yet synced; needs Steamworks networking.
- **Diesel for trucks** — design idea (no commits yet).
- **NPC walk‑out animation/voiceline** — idea stage (no commits yet).

---

## Release notes summary (suggested)

- **v1.0.0 (historical)** — Initial playable fuel system: consumption, gauge, save/load, stations groundwork. (main‑archived: 04f4394; later merged forward)
- **v1.1.0 (current master snapshot)** — Per‑vehicle capacities, dynamic pricing (daily + tier), refuel UX (gauge visible), improved gating/fixes, config toggles, default capacity set to 18 L.
