# Dungeon Slash - Quick Reference Guide

## 🎮 How to Play
- **WASD:** Move (camera-relative)
- **Space:** Jump
- **Left Mouse:** Light attack (15 dmg, 0.5s cooldown)
- **Right Mouse:** Heavy attack (30 dmg, 1.2s cooldown)
- **1/2/3:** Switch weapons
- **Click to advance** during opening cutscene

## 🏗️ Project Structure at a Glance

```
📦 learn-3d/DungeonSlash/
├── Assets/
│   ├── Scripts/             ← 26+ C# files, organized by system
│   ├── Scenes/             ← Main.unity (playable), SampleScene.unity (dev)
│   ├── Prefabs/            ← Player, enemies, projectiles
│   ├── Animations/         ← Animator controllers + Mixamo clips
│   ├── Materials/          ← URP materials with emission for flash
│   └── ThirdParty/         ← Little Heroes Mega Pack (character models)
└── ProjectSettings/        ← Unity 6.0.34f1 config
```

## 🎯 System Overview

### Player System
| Component | File | Role |
|-----------|------|------|
| **Movement** | `PlayerController.cs` | WASD, jump, gravity |
| **State Machine** | `PlayerState.cs` | Controls what actions are allowed |
| **Combat** | `PlayerCombat.cs` | Attack input + auto-aim |
| **Health** | `PlayerHealth.cs` | HP, knockback, feedback |
| **Weapons** | `WeaponManager.cs` | Runtime bone attachment + switching |
| **Animation** | `PlayerAnimator.cs` | Animator parameter management |

**Key Insight:** PlayerState acts as a permission gate. Example: Can't jump while attacking, can't move while hit.

### Enemy System
| Component | File | Role |
|-----------|------|------|
| **Base Class** | `EnemyBase.cs` | Shared AI state machine, health, feedback |
| **Melee Type** | `MeleeEnemy.cs` | Chase + melee attack (OverlapSphere) |
| **Ranged Type** | `RangedEnemy.cs` | Chase + ranged attack (projectiles) |
| **Projectile** | `Projectile.cs` | Simple linear movement + collision |

**AI States:** Idle → Chase → Attack → Hit → Dead

### Combat System
| Component | File | Role |
|-----------|------|------|
| **Interface** | `IDamageable.cs` | Contract: `TakeDamage(damage, direction)` |
| **Damage System** | `DamageSystem.cs` | Central dealing logic |
| **Weapon Data** | `WeaponData.cs` | ScriptableObject (tweaking without code) |
| **Hitbox** | `WeaponHitbox.cs` | OnTriggerEnter relay |

**Flow:** Input → auto-aim → activate collider → OnTriggerEnter → DamageSystem → IDamageable.TakeDamage()

### Game Flow
| Component | File | Role |
|-----------|------|------|
| **Game State** | `GameManager.cs` | Singleton: death/victory handling |
| **Room Control** | `RoomManager.cs` | Activate rooms, spawn enemies |
| **Cutscenes** | `CutsceneManager.cs` | Opening dialogue + camera swap |

## 💡 Key Design Patterns Explained

### 1️⃣ Priority-Based State Machine
```
Dead (4) > Hit (3) > Attacking (2) > Jumping (1) > Normal (0)
```
**Benefit:** Prevents invalid combos. Getting hit interrupts attack (higher priority).

### 2️⃣ Data-Driven Weapons
WeaponData.cs holds all stats → Designers can tweak without touching code.
```csharp
[CreateAssetMenu]  // Right-click in Project → Create Weapon
public class WeaponData : ScriptableObject
{
    public float lightDamage = 15f;
    public float lightHitDelay = 0.3f;  // When collision activates
    public float lightHitDuration = 0.2f;  // How long it stays active
}
```

### 3️⃣ Bone Attachment
Weapons follow hand bones automatically via hierarchy:
```
Animator plays attack → Hand bone moves → Weapon (child) follows
```

### 4️⃣ Interface-Based Damage
Both player and enemies implement IDamageable:
```csharp
public interface IDamageable
{
    void TakeDamage(float damage, Vector3 knockbackDirection);
}
```
→ Combat code never asks "is this a player or enemy?" Just calls TakeDamage().

### 5️⃣ Material Flash Feedback
Pre-instantiate materials in Awake() (not during combat):
```csharp
// Awake: Create instances
cachedMaterials[i] = renderers[i].material;

// On hit: Modify instance (doesn't affect others)
cachedMaterials[i].SetColor("_EmissionColor", white * 3f);
```
**Why?** URP + SRP Batcher don't support MaterialPropertyBlock. Material instances work.

## 📊 Combat Balance

### Player
- **Speed:** 6 m/s
- **Jump:** 1.5m height, 0.15s coyote time
- **Light Attack:** 15 dmg, 0.5s cooldown, hits at 0.3s
- **Heavy Attack:** 30 dmg, 1.2s cooldown, hits at 0.45s

### Melee Enemy
- **Health:** 30 HP
- **Speed:** 3.5 m/s
- **Detection:** 10m range
- **Attack:** 15 dmg, 1.5s radius, 0.4s startup

### Ranged Enemy
- **Health:** 30 HP
- **Speed:** 3.5 m/s
- **Preferred Distance:** 6m (retreats if closer)
- **Attack:** 10 dmg per projectile, 10 m/s speed, 5s lifetime

## 🔍 Performance Notes

✅ **Optimizations in place:**
- Animator hash caching (no string hashing per frame)
- Ground detection via CheckSphere (not controller.isGrounded)
- Physics.OverlapSphere for hitboxes (batch queries)
- Material instances pre-allocated at startup

⚠️ **Scalability considerations:**
- No object pooling (could add for 100+ enemies)
- No LOD system (distance culling)
- Single scene with all rooms (could split into scenes)

## 🚀 How to Extend

### Add a New Enemy Type
1. Create `NewEnemy.cs : EnemyBase`
2. Implement `PerformAttack()` method
3. Configure stats in Inspector

### Add a New Weapon
1. Right-click in Project → Create → Weapon Data
2. Set damage, cooldown, timings
3. Drag prefab into weaponData.prefab field
4. Add to WeaponManager.weapons array

### Add a New Room
1. Duplicate existing room in scene
2. Configure RoomManager.rooms[] array
3. Add player spawn point
4. Add enemy spawn points + prefabs

## 🐛 Common Issues & Fixes

| Issue | Cause | Fix |
|-------|-------|-----|
| Weapon doesn't follow hand | Bone name mismatch | Check WeaponManager rightHandBoneName |
| Attacks don't hit | Hitbox not triggering | Check weapon collider is set to Trigger |
| Enemy doesn't move | NavMesh not baked | Window → AI → Navigation, Bake |
| Player gets stuck in wall | ground check radius too small | Increase groundCheckRadius |

## 🎓 Interview Talking Points

**Q: Why CharacterController instead of Rigidbody?**
A: Direct control without physics complexity. Better for action games where movement feel matters.

**Q: How do you prevent attack spam?**
A: Three layers: (1) cooldown timer, (2) PlayerState prevents state entry, (3) check CanAttack before input.

**Q: How do weapons know when to deal damage?**
A: Timing from WeaponData. PlayerCombat counts frames and enables collider at right time.

**Q: How does enemy AI work?**
A: State machine (Idle/Chase/Attack/Hit). NavMeshAgent handles pathfinding automatically.

**Q: How do you scale this to 100 enemies?**
A: Add object pooling, LOD system, GPU instancing for visuals, batch physics queries.

---

**Full analysis:** See `PROJECT_ANALYSIS.md` for deep dive into architecture, code quality, and learning value.
