# Unity Project: Attack Input Handling Analysis
**Project Location:** `/Users/kuangjianwei/AI_Discover/learn-3d/DungeonSlash`

---

## Summary

### Input System Type: **OLD INPUT MANAGER** (NOT the new Input System)
The project uses Unity's legacy `Input.*` API calls, NOT the modern Input System package.

### Found .inputactions File
- **Location:** `DungeonSlash/Assets/InputSystem_Actions.inputactions`
- **Status:** Present but **NOT actively used** by the main game code
- **Note:** This appears to be template/reference file that was included but the actual game uses the old Input Manager

---

## 1. ATTACK INPUT READING LOCATIONS

### Primary Attack Input Handler: **PlayerCombat.cs**

**File:** `DungeonSlash/Assets/Scripts/Player/PlayerCombat.cs`

**Lines 95-102:** Direct attack input reading using OLD Input Manager

```csharp
// Line 95-102: Main attack input detection
if (Input.GetMouseButtonDown(0) && attackCooldownTimer <= 0f)
{
    StartLightAttack();
}
else if (Input.GetMouseButtonDown(1) && attackCooldownTimer <= 0f)
{
    StartHeavyAttack();
}
```

**Attack Input Keys:**
- **Left Mouse Button (Button 0):** Triggers `StartLightAttack()` (轻攻击 - Light Attack)
- **Right Mouse Button (Button 1):** Triggers `StartHeavyAttack()` (重攻击 - Heavy Attack)

**Attack Flow:**
1. `Update()` checks `Input.GetMouseButtonDown(0|1)` every frame
2. Guards: `attackCooldownTimer <= 0f` and `playerState.CanAttack`
3. Calls `StartLightAttack()` or `StartHeavyAttack()`
4. Sets `isAttacking = true` and schedules attack collision via timing

---

## 2. INPUT SYSTEM SETUP

### Current System: **Old Input Manager** ✓ CONFIRMED

**Evidence:**
- All attack input uses `Input.GetMouseButtonDown()` API
- No `InputAction` or `InputActionReference` callbacks
- No event-driven input (no `.performed` callbacks)

**Key Code References:**

| File | Method | Input API | Purpose |
|------|--------|-----------|---------|
| PlayerCombat.cs | Update() | `Input.GetMouseButtonDown(0)` | Light attack (left mouse) |
| PlayerCombat.cs | Update() | `Input.GetMouseButtonDown(1)` | Heavy attack (right mouse) |
| WeaponManager.cs | Update() | `Input.GetKeyDown(KeyCode.AlphaX)` | Weapon switching (1/2/3 keys) |
| CutsceneManager.cs | Update() | `Input.GetMouseButtonDown(0)` | Cutscene skipping |

---

### Why NOT using the new Input System?

Despite the `.inputactions` file existing in the project, the code uses the legacy API because:
1. The new Input System is more complex and better for complex control schemes
2. This project's input needs are simple (mouse clicks + keyboard)
3. Legacy Input Manager is simpler and sufficient for this game
4. The `.inputactions` file is likely included as template/documentation

**Recommendation:** If you need advanced features (input rebinding, complex gamepad schemes), migrate to the new Input System. Otherwise, keep current approach.

---

## 3. INPUTACTIONS FILE DETAILS

### File Location
`DungeonSlash/Assets/InputSystem_Actions.inputactions`

### File Analysis
- **Name:** InputSystem_Actions
- **Action Maps:** 2
  1. **Player Map** (Game Input)
  2. **UI Map** (User Interface Input)

### Player Action Map - Attack Action

```json
{
    "name": "Attack",
    "type": "Button",
    "id": "6c2ab1b8-8984-453a-af3d-a3c78ae1679a",
    "expectedControlType": "Button",
    "interactions": "",
    "initialStateCheck": false
}
```

### Attack Input Bindings (from .inputactions)

| Device | Control | Action | Groups |
|--------|---------|--------|--------|
| Gamepad | West Button (Y) | Attack | Gamepad |
| Mouse | Left Button | Attack | Keyboard&Mouse |
| Touchscreen | Primary Touch Tap | Attack | Touch |
| Joystick | Trigger | Attack | Joystick |
| XR Controller | Primary Action | Attack | XR |
| Keyboard | Enter Key | Attack | Keyboard&Mouse |

**Status:** This file is DEFINED but NOT ACTIVE in code. The game still uses old `Input.GetMouseButtonDown(0)`.

---

## 4. DETAILED FILE LISTING

### Files Related to Attack Input Handling

1. **PlayerCombat.cs** - PRIMARY ⭐
   - Location: `DungeonSlash/Assets/Scripts/Player/PlayerCombat.cs`
   - Contains: Direct attack input (`Input.GetMouseButtonDown(0|1)`)
   - Handles: Light/Heavy attack initiation, collision timing
   - Key Methods:
     - `Update()` - Input polling
     - `StartLightAttack()` - Line 105
     - `StartHeavyAttack()` - Line 119
     - `OnAttackHitStart()` - Collision enable
     - `OnAttackHitEnd()` - Collision disable

2. **PlayerState.cs** - STATE GUARD ⭐
   - Location: `DungeonSlash/Assets/Scripts/Player/PlayerState.cs`
   - Contains: `CanAttack` property (state-based guard)
   - Guards attack input from executing during Hit/Dead states
   - Priority System:
     ```
     Normal (0) → Jumping (1) → Attacking (2) → Hit (3) → Dead (4)
     ```

3. **PlayerAnimator.cs** - ANIMATION TRIGGER
   - Location: `DungeonSlash/Assets/Scripts/Player/PlayerAnimator.cs`
   - Called by: PlayerCombat.StartLightAttack() / StartHeavyAttack()
   - Methods:
     - `PlayLightAttack()` - Line 54 (uses SetTrigger)
     - `PlayHeavyAttack()` - Line 59 (uses SetTrigger)

4. **WeaponManager.cs** - WEAPON SWITCHING
   - Location: `DungeonSlash/Assets/Scripts/Player/WeaponManager.cs`
   - Uses: `Input.GetKeyDown(KeyCode.AlphaX)` (Lines 82-84)
   - Bindings: 1=Weapon1, 2=Weapon2, 3=Weapon3

5. **WeaponData.cs** - ATTACK CONFIGURATION
   - Location: `DungeonSlash/Assets/Scripts/Combat/WeaponData.cs`
   - ScriptableObject containing attack parameters
   - Attack Timing Fields:
     - `lightHitDelay` - When to activate collision box
     - `lightHitDuration` - How long collision stays active
     - `heavyHitDelay` - Heavy attack delay
     - `heavyHitDuration` - Heavy attack duration

6. **CutsceneManager.cs** - SECONDARY INPUT
   - Location: `DungeonSlash/Assets/Scripts/Cutscene/CutsceneManager.cs`
   - Uses: `Input.GetMouseButtonDown(0)` to skip cutscenes (Line 98)
   - Not part of combat system

7. **CameraController.cs** - EXAMPLE SCRIPT
   - Location: `DungeonSlash/Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`
   - Uses: Legacy input (mouse clicks for camera control)
   - Not part of main combat system

---

## 5. ATTACK INPUT FLOW DIAGRAM

```
Update() Called Every Frame
       ↓
[PlayerCombat.Update() - Line 56]
       ↓
✓ Check playerState.CanAttack [PlayerState.cs]
   ├─ Normal? ✓
   ├─ Jumping? ✓
   ├─ Attacking? ✓ (but won't restart)
   ├─ Hit? ✗ (blocks input)
   └─ Dead? ✗ (blocks input)
       ↓
✓ Check !playerController.InputEnabled [Cutscenes]
       ↓
[Line 95] Input.GetMouseButtonDown(0)?
       ├─ YES → StartLightAttack()
       │         ├─ Set isAttacking = true
       │         ├─ Set attackCooldownTimer
       │         ├─ playerState.EnterAttacking()
       │         ├─ playerAnimator.PlayLightAttack()
       │         └─ AutoAimToNearestEnemy()
       │
       └─ NO
           ↓
       [Line 99] Input.GetMouseButtonDown(1)?
           ├─ YES → StartHeavyAttack()
           │         ├─ Set isAttacking = true
           │         ├─ Set attackCooldownTimer
           │         ├─ playerState.EnterAttacking()
           │         ├─ playerAnimator.PlayHeavyAttack()
           │         └─ AutoAimToNearestEnemy()
           │
           └─ NO → Repeat next frame
```

---

## 6. ATTACK PARAMETERS & CONFIGURATION

### Light Attack (Left Mouse Button)
- **Damage:** WeaponData.lightDamage (default: 15f)
- **Cooldown:** WeaponData.lightAttackCooldown (default: 0.5s)
- **Hit Delay:** WeaponData.lightHitDelay (default: 0.3s)
- **Hit Duration:** WeaponData.lightHitDuration (default: 0.2s)
- **Weapon:** Left hand collider

### Heavy Attack (Right Mouse Button)
- **Damage:** WeaponData.heavyDamage (default: 30f)
- **Cooldown:** WeaponData.heavyAttackCooldown (default: 1.2s)
- **Hit Delay:** WeaponData.heavyHitDelay (default: 0.45s)
- **Hit Duration:** WeaponData.heavyHitDuration (default: 0.25s)
- **Weapon:** Right hand collider

---

## 7. STATE GUARDS & SAFEGUARDS

### Preventing Invalid Attacks

```csharp
// Guard 1: PlayerState state machine (PlayerState.cs:49)
if (!playerState.CanAttack) return;  // Blocks Hit/Dead states

// Guard 2: Cutscene control (PlayerCombat.cs:93)
if (playerController != null && !playerController.InputEnabled) return;

// Guard 3: Cooldown timer (PlayerCombat.cs:95, 99)
if (...&& attackCooldownTimer <= 0f)

// Guard 4: Already attacking (implicit in UpdateLoop)
if (isAttacking) { /* handle ongoing attack */ return; }
```

### Attack Interruption

**Hit Interruption (PlayerCombat.cs:65-70):**
```csharp
if (isAttacking) {
    // 受击打断攻击：如果在攻击过程中被打了，立刻取消攻击
    if (playerState.CurrentState == PlayerState.State.Hit ||
        playerState.CurrentState == PlayerState.State.Dead)
    {
        CancelAttack();
        return;
    }
}
```

---

## 8. WEAPON SWITCHING INPUT

**File:** `WeaponManager.cs` Lines 82-84

```csharp
if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchWeapon(0);
else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchWeapon(1);
else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchWeapon(2);
```

**Guard:** Won't switch during attack (`playerCombat.IsAttacking` - Line 116)

---

## KEY FINDINGS SUMMARY

✓ **Input System:** Old Input Manager (NOT new Input System)
✓ **Attack Input Methods:**
  - `Input.GetMouseButtonDown(0)` → Light Attack
  - `Input.GetMouseButtonDown(1)` → Heavy Attack
✓ **State Guards:** Comprehensive state machine prevents invalid attacks
✓ **Collision Timing:** Weapon collision box enabled/disabled based on animation timing
✓ **Input Actions File:** Exists but unused (reference/template)
✓ **Attack Interruption:** Automatically cancels when hit/dead
✓ **Auto-Aim:** Targets nearest enemy automatically within radius

---

## RECOMMENDATIONS FOR MODIFICATION

### To Switch to New Input System:
1. Install "Input System" package (if not already)
2. Create InputActionAsset (or use existing `InputSystem_Actions.inputactions`)
3. Modify PlayerCombat.cs to use `PlayerInputActions.Player.Attack.performed += OnAttack`
4. Remove `Input.GetMouseButtonDown()` calls

### To Add New Attack Types:
1. Add new `KeyCode` check in `PlayerCombat.Update()`
2. Or add new action to `.inputactions` file and follow event-driven pattern

### To Modify Attack Timing:
1. Edit WeaponData asset in Inspector
2. Adjust `lightHitDelay`, `lightHitDuration`, etc.

