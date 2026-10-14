# RPH – Revamped Project Hospital

Total-overhaul **code mod** for Project Hospital (Oxymoron Games, Unity): economy, recruitment/HR,
equipment, departments and research. The first deliverable is an "Advanced statistics" window; it
is the instrument panel used to build and balance every later gameplay change.
Plan and rationale: [docs/ROADMAP.md](docs/ROADMAP.md).

## Language

- Talk to the user in French.
- Code, identifiers, comments, commit messages and XML IDs are in English.
- Player-facing text goes through the game's string tables (EN + FR, `mod/Database/<Module>/`). Never hard-code UI strings.

## Game facts (verified by decompiling 1.2.x, October 2026)

- Unity **2018.2.17f1**, Mono, `mscorlib` 2.0 profile. Mods target **.NET Framework 3.5** (`net35`).
  Use `LangVersion` 7.3. Not available: `ValueTuple`, `Task`/`async`, `Lazy`, `Span`.
  The game's DLLs nevertheless reference `mscorlib` 4.0.0.0: `RPH.Mod.csproj` sets
  `_FindDependencies=false` so MSBuild accepts them (Mono unifies the versions at runtime).
- The macOS build is x86_64 (runs under Rosetta on Apple Silicon).
- Game code is in `Assembly-CSharp.dll`: simulation in namespace `Lopital`, UI controllers in the
  global namespace.
- UI is legacy uGUI (`UnityEngine.UI.Text`). The game UI does not use TextMeshPro.

## Coding rules
- **Think before you code.** Voice your assumptions aloud. If the requirement is ambiguous, ask questions. If there’s a simpler approach, insist on it. Stop when you’re lost, identify what’s unclear—don’t just pick a random interpretation and dive in.

- **Simplicity above all.** Write the minimum amount of code necessary to solve the problem. No speculative abstractions. No flexibility that nobody asked for. The test: Would a senior engineer find this too complicated?

- **Surgical changes.** Change only what the task requires. Don’t improve the surrounding code.

- **Goal-oriented execution.** Transform vague instructions into verifiable goals before writing a single line of code. “Add validation” becomes “write tests for invalid inputs, then make sure they pass.”

### Native code-mod loader (`TitleScreenController.LoadAddons`)

- Scans only `<StreamingAssets>/Addons/*/`. The game never loads DLLs from Workshop folders
  (Workshop subscriptions only deliver XML). Players must copy the mod into `Addons/` by hand.
- A folder's DLLs load only if it contains `0Harmony.dll` **and** the player enabled
  Options → "Enable loading of code mods" (PlayerPrefs key `OPTIONS_CODE_MODS_ENABLED`).
- Every `*.dll` except `0Harmony.dll` is `Assembly.LoadFrom`'d. Every type is registered in
  `HospitalMod.sm_modTypes[type.Name]` (**short name**: prefix the public types of `RPH.Mod` with `RPH`;
  `RPH.Core` is loaded the same way but its names only matter if they clash with a `ProcedureScript`).
- A type named `ModEntryPoint` or deriving from `HospitalMod` is added as a component on a
  `DontDestroyOnLoad` GameObject. That is our entry point.
- XML `ProcedureScript` names resolve to `Lopital.<Name>` first, then to `sm_modTypes`: a mod can
  add procedure scripts.
- `Database/ModAddon.xml` in the mod folder is read, then the folder's XML database.
- **BepInEx is not needed.** Don't depend on it. Other mods use it (Hospital EMS by Loupito75,
  HZDev's QoL Mod): stay compatible, never require it.

### Paths (macOS)

| What | Path |
|---|---|
| Game bundle | `~/Library/Application Support/Steam/steamapps/common/Project Hospital/project_hospital.app` |
| Managed DLLs | `<bundle>/Contents/Resources/Data/Managed/` |
| StreamingAssets | `<bundle>/Contents/Resources/Data/StreamingAssets/` (Database, Textures, Addons, DLCs) |
| Install target | `<StreamingAssets>/Addons/ModRPHRevamped/` |
| Player log | `~/Library/Logs/Unity/Player.log` |
| Balance logs | `~/Library/Application Support/unity.Oxymoron Games.Project Hospital/RPH/Logs/balance-*.log` (`Application.persistentDataPath`) |
| Saves | `~/Library/Application Support/unity.Oxymoron Games.Project Hospital/maps/` |
| Workshop | `~/Library/Application Support/Steam/steamapps/workshop/content/868360/` |

On Windows the game is under `steamapps/common/Project Hospital/ProjectHospital_Data/` and the log
under `%USERPROFILE%\AppData\LocalLow\Oxymoron Games\Project Hospital\`.
Never hard-code these paths in code. The build reads `GameDir` from a git-ignored `local.props`.

### Hook points

Line numbers drift between game versions: re-grep the decompiled code after every game update.

| System | Class / member | Notes |
|---|---|---|
| All money in | `Lopital.Hospital.Pay(int, PaymentCategory)` | Single choke point. |
| Money in by department | `Lopital.Department.Pay(int, PaymentCategory, Entity)` | Updates dept stats, then calls `Hospital.Pay`. Gives department + patient. |
| All money out | `Lopital.Hospital.Spend(int, ExpenseCategory)` | No department context. |
| Wages | `Department.PayWages(Shift)` → private `PayWages(EntityIDPointer<Entity>, Shift)` | Clinic vs hospitalization split by home room. |
| Midnight | `Hospital.OnDayStart` | Daily costs (loan interest, ambulance upkeep). Runs before `SwitchToNewStatistics` closes the day. |
| Hospitalization | `HospitalizationComponent.m_state` | Type in `m_hospitalizationTreatment` (`TRT_HOSPITALIZATION_*`), bed in `m_bed`. Diagnosis paid once in private `SendHome`. |
| Ambulance arrival | `Ambulance.SetupPatient` | Own ambulance and helicopter only; city ambulances (`m_external`) hand over the patient elsewhere. |
| Daily stats | `Lopital.DailyStatistics` | Hospital: `Hospital.m_state.m_dailyStatistics`. Dept: `m_departmentPersistentData.m_todaysStatistics` / `m_statistics`. Aggregates only, no ledger. |
| Budget, loan | `Lopital.Budget` | Starts at 100 000, loan steps of 20 000, max 500 000. Interest (loan / 100) charged in `Hospital.OnDayStart`. |
| Prestige | `DailyStatistics.m_reputation` (0–1) | Daily score; closed days in `Hospital.m_state.m_statisticsHistory`. Patient count modifier: `Hospital.GetPrestigePatientsModifier(float)`. |
| Patient spawn | `InsuranceCompany.CalculatePatientCounts`, `BehaviorPatient.RandomizeMedicalCondition` | Daily counts per insurer (walk-in, ambulance, helicopter index); diagnosis drawn per department, weighted by `GameDBDiagnosisOccurrence`. |
| Salary | `Lopital.EmployeeComponent` (`SalaryBase + …`) | Fixed at hire. |
| Hiring | `Lopital.HiringManager`, `HiringPanelController` | Agency prices hard-coded (2 500 / 1 000). |
| Insurance | `Lopital.InsuranceManager`, `Lopital.InsuranceCompany` | Objectives and rewards. |
| Management panel | `HospitalManagementPanelController` | Tabs: Departments, Insurance, Statistics, Budget, Ambulances. |
| Tweakables | `<StreamingAssets>/Database/Tweakables.xml` (+ `TweakablesPatch38/39.xml`) | 89 values, overridable from mod XML. |

Quirks: `Spend(…, HIRING_AGENCY)` is booked in `m_expensesPenalties`. In vanilla, exams and
treatments cost the hospital nothing and equipment has no upkeep.

### Reusing game assets

- Load game UI prefabs at runtime: `Resources.Load("Prefabs/UI/<Name>")`, e.g. `TableItemText`,
  `TableItemIcon`, `TableItemIconText`, `TableRowSeparator`, `TableScrollbarIndicator`,
  `SegmentPanelHeading`, `SegmentPanelWithSeparator`, `SegmentItemMedium`, `SegmentItemPercentLarge`,
  `TextSmallWhite`. Icon atlas: `<StreamingAssets>/Textures/ui/icons.png`.
- Load game assets from the player's install at runtime. **Never copy game textures, DLLs or
  decompiled code into this repo** (MIT repo; that content belongs to Oxymoron Games).
- Own assets must be our own work or CC0, each listed in `CREDITS.md`. Workshop mods without a
  license are all rights reserved: don't copy code or assets from other mods (PooK, Bowelas, CRP…).
  Graphics format: see the isometric atlas specs in the official modding tutorials.

## Decompiling

```bash
dotnet tool install ilspycmd --tool-path .tools
DOTNET_ROLL_FORWARD=Major .tools/ilspycmd -p -o decompiled "<Managed>/Assembly-CSharp.dll"
```

`decompiled/` and `.tools/` are git-ignored. Read them to find hook points; never commit them.

## Architecture

One folder = one module, in the code and in the game data. Shared pieces live at the root of
each project or in `Common/`.

```
src/RPH.Core/          pure C#, no Unity/game refs; net35 (shipped) + netstandard2.0 (tested).
  Accounting/          shared: ledger, recorder, reports, sidecar format (every module reads money)
  Formatting/          shared: money text
src/RPH.Mod/           net35; refs game DLLs + Harmony.
  RPH*.cs              shared: entry point (RPHModEntry : HospitalMod), log, texts, game data, compat
  Accounting/          shared: ledger service + the patches that feed it (money, activity, save)
  UI/                  shared: uGUI kit in the game's light theme (RPHUi)
  Stats/               module: advanced statistics window + its tab button
  <Module>/            next modules: Economy/, HR/, Equipment/, Research/...
tests/RPH.Core.Tests/  xUnit on net10.0, tests RPH.Core only. CI runs them (.github/workflows).
mod/Database/          shipped as-is; the game reads Database/ recursively.
  ModAddon.xml         addon declaration
  Common/              shared strings (categories, roles...), StringTableEn/Fr.xml
  <Module>/            the module's XML: strings (StringTableEn/Fr.xml, ID LOC_<LANG>_RPH_<MODULE>), data
docs/                  roadmap.
```

A module's code is in namespace `RPH.Mod.<Module>` and its own patches sit in its folder. A module
may use the shared folders, never another module's folder: move shared code to the shared folders
when a second module needs it.

Rules:

- One Harmony instance, id `com.drekth.rph`. Prefer `Prefix`/`Postfix` over transpilers.
  Every patch body catches and logs its exceptions: a patch must never break the simulation.
- Game logic stays in `RPH.Core` behind plain data types so it can be unit-tested without Unity.
- Gameplay features (phase 3+) get an on/off switch. A disabled feature leaves vanilla behavior
  untouched. Phases 1–2 only observe and need none.
- Save compatibility: never add our types to the game's save. Store our state in a sidecar file
  next to the save (`maps/<save>.rph.txt`, tab-separated, see `LedgerSerializer`). Removing the mod
  must leave saves loadable.
- Every gameplay number lives in data (config or XML tweakables), never in code.
- Performance: no per-frame work in the simulation path. Record events, aggregate on hour/day
  ticks, refresh UI only while it is open.
- Log through one logger with the `[RPH]` prefix.
- Compatibility: PooK Hospital Economy patches the same money paths and adds its own price list.
  Detect it and warn; never charge twice. Detect Hospital EMS; don't require it.
  Features inspired by other mods (Loupito75's, Bottleneck Monitor, PooK) are rewritten from their
  public descriptions: never read or copy their code. A gameplay feature inspired by a mod turns
  itself off when that mod is loaded.

## Build and verify

```bash
dotnet build src/RPH.Mod -c Release
```

```bash
dotnet test tests/RPH.Core.Tests
```

First time: `cp local.props.example local.props` (and fix `GameDir` if needed). A Release build
copies the mod to the install target. Check in game: `Player.log` contains `[RPH] loaded`,
`[RPH] Harmony: N patches applied, 0 failed` and `[RPH] Ledger ready`. F9 opens the window.
Any `[RPH] Day N: ... not explained by the ledger` line means a money path is not hooked.
For economy features, the totals shown by our window must match the game's Budget tab to the dollar.

### Balance log

`RPHLog.Balance(section, data)` writes one file per game launch (path above). Tab-separated columns:
real time, game day (`D12`), game hour, section, then space-separated `key=value` pairs. Sections are the
constants of `BalanceSection` (`RPHLog.cs`): SESSION (new/load/save, version, other mods), CONFIG (every
`RPH_*` tweakable in effect, at session start), DAY and MONEY (closed day: totals, patients, prestige, one
line per ledger category), RENOWN, PATIENTS, DIAGNOSIS, INPATIENT, AMBULANCE, LOANS, EXAMS, MAINTENANCE.
Filter with `grep -P "\tLOANS\t" balance-*.log`. A new feature that needs balancing logs its numbers there
(daily summaries at midnight rather than per event when events are frequent), and adds its section.

## Third-party

- Harmony (Andreas Pardeike, MIT) ships as `0Harmony.dll` with its license file. Use the net35 build
  of **Lib.Harmony 2.2.2**. Do not upgrade: 2.3+ (MonoMod `Helpers.GetOrInitWithLock<T>`) crashes the
  macOS game's Mono at patch time (`Assertion at mini-generic-sharing.c:148`), even though it works on Windows.
- Community Resource Pack (Workshop) is unmaintained since about 2022. Consider it only for new
  medical content (roadmap phase 7), as an optional dependency.
