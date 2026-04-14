# Attack Input System Analysis - Complete Documentation

**Project:** learn-3d (DungeonSlash)  
**Analysis Date:** April 2026  
**Status:** Complete ✅

---

## 📚 Documentation Files

This folder contains comprehensive analysis of the attack input handling system:

### 1. **ATTACK_INPUT_ANALYSIS.md** (10 KB)
The complete technical deep-dive. Start here for full understanding.
- **Contains:**
  - Detailed input reading locations
  - Input system type confirmation
  - .inputactions file analysis
  - Complete file listing with annotations
  - Attack flow diagrams
  - State machine documentation
  - Guard & safeguard explanations
  - Modification recommendations

### 2. **ATTACK_INPUT_QUICK_REFERENCE.md** (4.5 KB)
Quick lookup format for fast reference. Use this while coding.
- **Contains:**
  - Input summary table
  - Key files (prioritized 1-6)
  - State guards checklist
  - Attack flow (simplified)
  - Attack stats (defaults)
  - Common issues & fixes
  - Debug checklist

### 3. **ATTACK_INPUT_ARCHITECTURE.txt** (19 KB)
Visual ASCII diagrams showing system architecture and data flow.
- **Contains:**
  - Layer-by-layer architecture diagram
  - Input to damage data flow
  - State machine visual representation
  - Weapon switching flow
  - Attack timing timeline
  - Collision timing diagram

### 4. **FILES_SUMMARY.txt** (13 KB)
Complete file catalog with line-by-line annotations.
- **Contains:**
  - All critical files listed with priority
  - Code line references
  - Method listings
  - Parameter defaults
  - Guard execution order
  - Modification guide

### 5. **README_ATTACK_INPUT.md** (this file)
Index and navigation guide.

---

## 🎯 Quick Start

### I need to understand the attack system (10 minutes)
→ Read: **ATTACK_INPUT_QUICK_REFERENCE.md**

### I need to modify attack behavior (15 minutes)
→ Read: **FILES_SUMMARY.txt** → "WHERE TO MODIFY" section

### I need to understand the architecture (30 minutes)
→ Read: **ATTACK_INPUT_ARCHITECTURE.txt**

### I need full technical details (1 hour)
→ Read: **ATTACK_INPUT_ANALYSIS.md** (all sections)

### I'm looking for a specific file (5 minutes)
→ Read: **FILES_SUMMARY.txt** → "CRITICAL FILES" section

---

## 🔑 Key Findings Summary

### Input System
- **Using:** Old Input Manager (not new Input System)
- **API:** `Input.GetMouseButtonDown(0|1)`
- **File:** `PlayerCombat.cs` lines 95, 99

### .inputactions File
- **Location:** `DungeonSlash/Assets/InputSystem_Actions.inputactions`
- **Status:** Present but UNUSED
- **Type:** Template/Reference file
- **Game Uses:** Legacy Input Manager instead

### Attack Input Methods
```
Left Mouse (Button 0)  → StartLightAttack()  → Damage: 15, Cooldown: 0.5s
Right Mouse (Button 1) → StartHeavyAttack() → Damage: 30, Cooldown: 1.2s
```

### State Guards
```
BLOCKED:  Hit state, Dead state
ALLOWED:  Normal, Jumping, Attacking
PRIORITY: State → Cutscene → Cooldown → AlreadyAttacking → Input
```

---

## 📍 Essential Files

| Priority | File | Purpose | Lines |
|----------|------|---------|-------|
| ⭐⭐⭐ | PlayerCombat.cs | Main attack logic | 95, 99, 105, 119 |
| ⭐⭐ | PlayerState.cs | State guards | 49, 80, 92 |
| ⭐⭐ | WeaponManager.cs | Weapon switching | 82-84, 116 |
| ⭐ | PlayerAnimator.cs | Animation triggers | 54, 59 |
| ⭐ | WeaponData.cs | Attack parameters | ScriptableObject |
| ⭐ | WeaponHitbox.cs | Collision detection | OnTriggerEnter() |

---

## ⚡ Attack Flow (30-second version)

```
1. Input.GetMouseButtonDown(0|1) detected
2. PlayerState checks: CanAttack? (blocks Hit/Dead)
3. Cooldown check: expired?
4. StartLightAttack() or StartHeavyAttack()
5. Set isAttacking = true
6. Play animation
7. After 0.3s (delay): Enable collision
8. After 0.2s more (duration): Disable collision
9. Return to idle
```

---

## 🛡️ Why Attacks Sometimes Don't Work

**Check in order:**
1. ❌ Currently attacking? → Wait for animation to finish
2. ❌ Recently hit? → Wait for hit stun (0.5s)
3. ❌ Dead? → Permanent block
4. ❌ In cutscene? → Wait for cutscene to end
5. ❌ Weapon equipped? → Switch weapon first
6. ❌ On cooldown? → Wait (0.5-1.2s depending on attack)

---

## 🔧 Common Modifications

### Add new attack key
**File:** `PlayerCombat.cs` → `Update()` method
```csharp
else if (Input.GetMouseButtonDown(2)) StartSpecialAttack();
```

### Change attack damage
**File:** Inspector → Select weapon asset → Edit `WeaponData`
- Modify: `lightDamage`, `heavyDamage`

### Change cooldown
**File:** Inspector → Weapon asset
- Modify: `lightAttackCooldown`, `heavyAttackCooldown`

### Change hit timing
**File:** Inspector → Weapon asset
- Modify: `lightHitDelay`, `lightHitDuration`

### Disable input
**File:** Any controller
```csharp
playerController.InputEnabled = false;
```

---

## 📊 Attack Stats (Defaults)

### Light Attack
- **Input:** Left Mouse Button
- **Damage:** 15
- **Cooldown:** 0.5s
- **Hit Delay:** 0.3s (when collision starts)
- **Hit Duration:** 0.2s (how long collision stays active)
- **Weapon:** Left hand

### Heavy Attack
- **Input:** Right Mouse Button
- **Damage:** 30
- **Cooldown:** 1.2s
- **Hit Delay:** 0.45s
- **Hit Duration:** 0.25s
- **Weapon:** Right hand

### Weapon Switching
- **Input:** Keys 1, 2, 3
- **Guard:** Won't switch during attack
- **Effect:** Dual-wield switches both hands simultaneously

---

## 🎯 Advanced Topics

### State Machine (5 priorities)
```
Normal (0) ← Base state, anything goes
Jumping (1) ← Can attack, can't double-jump
Attacking (2) ← Can move upper/lower body separately
Hit (3) ← Locks all input until stun ends
Dead (4) ← Terminal state
```

### Auto-Aim System
- **Location:** `PlayerCombat.AutoAimToNearestEnemy()`
- **Radius:** 5 meters (configurable)
- **Behavior:** Snaps character rotation to nearest enemy
- **Purpose:** Improves combat feel in top-down action game

### Collision Timing
- Early frames of animation → collision box disabled
- At `lightHitDelay` → collision box enabled
- Enemies can't be hit until collision window
- Multiple hits same attack prevented by tracking

### Dual-Wield System
- Each weapon instance has own collider
- Left hand = light attacks (weaker, faster)
- Right hand = heavy attacks (stronger, slower)
- Both hands can use different rotation/position offsets

---

## 🐛 Debugging

### Enable debug output
Add to `PlayerCombat.Update()`:
```csharp
Debug.Log($"CanAttack: {playerState.CanAttack}");
Debug.Log($"IsAttacking: {isAttacking}");
Debug.Log($"CooldownTimer: {attackCooldownTimer}");
Debug.Log($"InputEnabled: {playerController.InputEnabled}");
```

### Verify collision box
1. Select weapon in Scene view
2. Check gizmos (green wireframe visible even when disabled)
3. Adjust collider center/size in Inspector

### Test state transitions
1. Watch Console for state changes
2. Use different attack types (light vs heavy)
3. Get hit during attack → should cancel
4. Die during attack → should cancel

---

## ✅ Verification Checklist

- [x] Attack inputs use `Input.GetMouseButtonDown(0|1)`
- [x] State machine prevents invalid attacks
- [x] Collision timing is frame-based
- [x] .inputactions file exists but unused
- [x] Weapon switching works with numeric keys
- [x] Auto-aim targets nearest enemy
- [x] Hit/Dead states interrupt attacks
- [x] Dual-wield with independent colliders
- [x] Weapon parameters configurable via ScriptableObject

---

## 📞 Quick Links

| Question | Answer | File |
|----------|--------|------|
| Where is attack input read? | PlayerCombat.cs line 95, 99 | ATTACK_INPUT_ANALYSIS.md |
| What system is used? | Old Input Manager | ATTACK_INPUT_ANALYSIS.md §2 |
| Is there a .inputactions file? | Yes, but unused | ATTACK_INPUT_ANALYSIS.md §3 |
| How do I change attack damage? | Edit WeaponData asset | ATTACK_INPUT_QUICK_REFERENCE.md |
| How do I add new attack? | Modify PlayerCombat.cs | FILES_SUMMARY.txt "WHERE TO MODIFY" |
| Why can't I attack? | Check guards in order | ATTACK_INPUT_QUICK_REFERENCE.md §4 |
| What's the attack flow? | See diagram | ATTACK_INPUT_ARCHITECTURE.txt §2 |

---

## 📝 Document Usage Rights

These analysis documents were generated for the `learn-3d/DungeonSlash` project. 
Feel free to:
- ✅ Reference in code reviews
- ✅ Share with team members
- ✅ Update with new findings
- ✅ Use as onboarding documentation
- ✅ Reference in commit messages

---

**Generated:** April 14, 2026  
**Analyzer:** Claude Code  
**Project:** /Users/kuangjianwei/AI_Discover/learn-3d
