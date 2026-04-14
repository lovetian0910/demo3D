# Attack Input System - Quick Reference

## 🎮 INPUT SUMMARY

| Input | File | Method | Line | Action |
|-------|------|--------|------|--------|
| **Left Mouse** | PlayerCombat.cs | GetMouseButtonDown(0) | 95 | Light Attack |
| **Right Mouse** | PlayerCombat.cs | GetMouseButtonDown(1) | 99 | Heavy Attack |
| **Key 1** | WeaponManager.cs | GetKeyDown(Alpha1) | 82 | Switch Weapon 1 |
| **Key 2** | WeaponManager.cs | GetKeyDown(Alpha2) | 83 | Switch Weapon 2 |
| **Key 3** | WeaponManager.cs | GetKeyDown(Alpha3) | 84 | Switch Weapon 3 |

---

## 🔧 SYSTEM TYPE

**Using:** Unity's OLD Input Manager (Legacy API)  
**NOT Using:** New Input System  
**Inputactions File:** Present but unused

```csharp
// What's used:
Input.GetMouseButtonDown(0)
Input.GetKeyDown(KeyCode.Alpha1)

// What's NOT used:
InputAction.performed += handler
```

---

## 📍 KEY FILES

### Must Know (Attack System)
1. **PlayerCombat.cs** - Main attack logic ⭐⭐⭐
2. **PlayerState.cs** - State guards ⭐⭐
3. **WeaponManager.cs** - Weapon switching

### Support Files
4. PlayerAnimator.cs - Animation triggers
5. WeaponData.cs - Attack parameters
6. WeaponHitbox.cs - Collision detection

---

## 🛡️ STATE GUARDS (Why attacks sometimes don't work)

```
Can Attack? Check these:
✓ playerState.CanAttack        // Not in Hit/Dead state
✓ !playerController.InputEnabled // Not in cutscene
✓ attackCooldownTimer <= 0      // Cooldown expired
✓ !isAttacking                  // Not already attacking
```

If ANY of these fail, attack is blocked.

---

## ⚡ ATTACK FLOW (Fast Reference)

```
Input.GetMouseButtonDown(0)
    ↓
playerState.CanAttack? (Hit/Dead blocks it)
    ↓
attackCooldownTimer <= 0?
    ↓
StartLightAttack()
    ├─ isAttacking = true
    ├─ playerState.EnterAttacking()
    ├─ PlayAnimation()
    ├─ AutoAim()
    └─ Enable collision after delay
```

---

## 🎯 ATTACK STATS (from WeaponData)

### Light Attack (Left Mouse)
```
lightDamage: 15
lightAttackCooldown: 0.5s
lightHitDelay: 0.3s    ← Wait 0.3s before collision
lightHitDuration: 0.2s ← Keep collision active 0.2s
```

### Heavy Attack (Right Mouse)
```
heavyDamage: 30
heavyAttackCooldown: 1.2s
heavyHitDelay: 0.45s   ← Longer wind-up
heavyHitDuration: 0.25s
```

---

## 🔍 WHERE TO MODIFY

### Add New Attack Input
**File:** PlayerCombat.cs, Update() method
```csharp
else if (Input.GetMouseButtonDown(2) && attackCooldownTimer <= 0f)
{
    StartSpecialAttack();
}
```

### Change Attack Damage
**File:** Inspector → Select weapon asset → Edit WeaponData

### Change Cooldown
**File:** WeaponData → lightAttackCooldown / heavyAttackCooldown

### Change Hit Timing
**File:** WeaponData → lightHitDelay (when collision starts)

### Block Input During Cutscene
**File:** PlayerController → Set InputEnabled = false

---

## ❌ COMMON ISSUES

| Problem | Cause | Fix |
|---------|-------|-----|
| Can't attack in Hit state | State guard working | ✓ Intended |
| Attack doesn't trigger | Cooldown running | Wait 0.5-1.2s |
| No collision damage | hitDelay not reached yet | ✓ Normal, timing-based |
| Can attack during cutscene | InputEnabled not checked | Set `playerController.InputEnabled = false` |
| Weapon doesn't switch mid-attack | Guard check | ✓ Intended (IsAttacking check) |

---

## 📊 INPUT PRIORITY

1. **State (Highest)** - Hit/Dead = NO input
2. **Cutscene** - InputEnabled flag
3. **Cooldown** - Timer check
4. **Already Attacking** - isAttacking flag
5. **Button Press (Lowest)** - Actual input

Higher priority = checked first and blocks lower priority

---

## 🎬 ANIMATION SYSTEM

```csharp
playerAnimator.PlayLightAttack()    // SetTrigger("LightAttack")
playerAnimator.PlayHeavyAttack()    // SetTrigger("HeavyAttack")

// Triggers (not Bools) ensure animation doesn't loop:
// - Animation plays once
// - Auto-resets trigger
// - Can't accidentally play twice
```

---

## 🎛️ DEBUG CHECKS

To verify system is working:
```csharp
// In PlayerCombat.Update():
Debug.Log($"CanAttack: {playerState.CanAttack}");
Debug.Log($"IsAttacking: {isAttacking}");
Debug.Log($"CooldownTimer: {attackCooldownTimer}");
Debug.Log($"InputEnabled: {playerController.InputEnabled}");
```

---

## 📝 NOTES

- ⚠️ **NOT using new Input System** despite `.inputactions` file existing
- ✓ Using old `Input.GetMouseButtonDown()` API
- ✓ State machine prevents invalid attacks effectively
- ✓ Weapon collision timing is frame-perfect (0.3s delay on light attack)
- ✓ Auto-aim snaps to nearest enemy within 5m radius (configurable)

