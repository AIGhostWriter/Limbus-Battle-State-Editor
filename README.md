# Limbus Battle State Editor

A BepInEx mod for Limbus Company that lets you inject buffs and abilities onto any unit in real time during combat.

---

### 🛠️ 1. Installation & Execution

* **Installation**: Place the `.dll` file into the `BepInEx\plugins` folder within your game directory.
* **Toggle UI**: Press **`F8`**

---

### 🔑 2. Requirements

| | Version |
|---|---|
| BepInEx (IL2CPP) | 6.x |
| Game | Limbus Company (Steam) |

> The panel is only functional during combat. The unit list will not populate outside of a battle scene.

---

### ⚔️ 3. Buff Injection

**Step 1: Select a Unit**
* Choose a faction (**Player** / **Enemy**) using the toggle at the top of the panel.
* Click any unit button that appears to select it as the injection target. The selected unit will be shown in brackets `[Name]`.

**Step 2: Configure Parameters**
* **Stack**: The stack count to apply (default: `1`).
* **Turn**: The duration in turns (default: `3`).

**Step 3: Find & Apply a Buff**
* **Type Filter**: Use the category buttons — **All / 버프 / 디버프 / 죄악 / 기타** — to narrow down the list.
* **Search**: Type a buff name (Korean) or keyword ID into the Search field to filter in real time.
* **Apply**: Click **`[+]`** next to any buff to inject it onto the selected unit immediately.
* **Pagination**: Use `<` / `>` to navigate through the full list of 500+ buff keywords.

---

### 🧠 4. Ability Injection

**Step 1: Select a Unit**
* Same as above — select a faction and click a unit to target it.

**Step 2: Configure Parameters**
* **Stack**: The ability value/magnitude (default: `1`).
* **Turn**: The duration in turns (default: `3`).

**Step 3: Find & Apply an Ability**
* **Search**: Type an ability ID or description into the Search field.
* **Apply**: Click **`[+]`** next to any ability to inject it onto the selected unit immediately.

---

### 📋 Available Abilities (Reference)

| ID | Effect |
|----|--------|
| `DefenseAdder` | Defense level +stack |
| `MaxSpeedAdder` | Max speed +stack |
| `MinSpeedAdder` | Min speed +stack |
| `Shield_NextTurn` | Shield +stack next turn |
| `Immortal` | Prevents instant death |
| `ForceHeadOnAllCoinInAllSlots` | Force all coins heads |
| `ForceHeadOnParrying` | Force clash coins heads |
| `ForceTailOnParrying` | Force clash coins tails |
| `EgoResourceAdder` | E.G.O resource +stack |
| `BlockMentalCorrision` | Block E.G.O erosion |
| `IsTargetableFalse` | Unit cannot be targeted |
| `IsActionableFalse` | Unit cannot act |
| `BreakOnRoundEnd` | Stagger at round end |

> Full ability list is browsable in the **Ability** tab of the panel.

---

### ⚠️ Notes

* Only works during a battle scene. The unit list will be empty on any other screen.
* Game updates may change `BUFF_UNIQUE_KEYWORD` or `SYSTEM_ABILITY_KEYWORD` enum values, which could break injection for affected keywords.
* Not recommended for use in any multiplayer or shared-session context.
* For personal testing
