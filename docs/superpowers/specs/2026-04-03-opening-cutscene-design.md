# Opening Cutscene System — Design Spec

**Date:** 2026-04-03
**Project:** Dungeon Slash

---

## Overview

An opening cutscene plays at scene load before gameplay begins. The camera cuts to close-up shots of each speaking character in turn, the character plays a fitting animation, and a dialogue line appears on screen. The player clicks to advance each line. After the final line, the camera snaps back to the overhead follow view and gameplay starts.

---

## User Experience Flow

1. Scene loads — player and enemies are in place but frozen
2. Camera cuts to Player close-up (front-facing, head-level)
3. Player plays `Relax` animation; first line of text appears instantly
4. Player clicks left mouse button → next line (same or different speaker)
5. If speaker changes, CutsceneCamera Follow/LookAt switches to the new character; Cinemachine Blend animates the transition
6. After the last line is clicked, dialogue UI hides, camera returns to overhead FollowCamera, player input and enemy AI re-enable

---

## Data Layer

**File location:** `Assets/StreamingAssets/dialogue/opening.json`

**Schema:**
```json
{
  "lines": [
    { "speaker": "player",  "text": "Darkness has consumed the dungeon...",      "animation": "Relax" },
    { "speaker": "player",  "text": "I alone must descend into this abyss.",      "animation": "Nod Head" },
    { "speaker": "enemy_0", "text": "Another fool walks to their death.",         "animation": "Clapping" },
    { "speaker": "enemy_0", "text": "We will crush you.",                         "animation": "Shake Head" }
  ]
}
```

**Speaker convention:**
- `"player"` → GameObject with tag `Player`
- `"enemy_0"` / `"enemy_1"` / `"enemy_2"` → GameObjects with tag `Enemy`, resolved by index in `FindGameObjectsWithTag` result

**Animation name convention:**
Value matches the Animator State name exactly (e.g. `"Relax"`, `"Nod Head"`, `"Clapping"`). These map directly to clip names from Little Heroes Mega Pack with the `Base@` prefix stripped.

**Recommended animation choices:**

| Situation | Animation | Character |
|-----------|-----------|-----------|
| Calm narration | `Relax` | Player |
| Resolve / decision | `Nod Head` | Player |
| Defensive stance | `Defend` | Player |
| Enemy taunting | `Clapping` | Enemy |
| Enemy dismissive | `Shake Head` | Enemy |
| Enemy threatening | `Melee Right Attack 01` | Enemy |
| Fallback | `Idle` | Any |

---

## Camera System

### Cameras in scene

| Camera | Priority (cutscene) | Priority (gameplay) | Purpose |
|--------|--------------------|--------------------|---------|
| `FollowCamera` (existing) | 0 | 10 | Overhead follow cam |
| `CutsceneCamera` (new) | 10 | 0 | Close-up cutscene shots |

### CutsceneCamera configuration

- **Follow Offset:** `(0, 1.5, -2.0)` — in front of character at head height
- **Rotation:** ~10° downward tilt to frame the face
- Cinemachine Brain **Default Blend:** `EaseInOut, 0.3s`

### Switching logic

- **Per line:** Set `CutsceneCamera.Follow` and `CutsceneCamera.LookAt` to the speaker's Transform. Cinemachine detects the target change and blends automatically.
- **End of cutscene:** Set `CutsceneCamera.Priority = 0`, `FollowCamera.Priority = 10`. Cinemachine blends back to overhead view.

---

## Scripts

### New scripts (`Assets/Scripts/Cutscene/`)

**`CutsceneManager.cs`** — Singleton. Owns the cutscene lifecycle.

Responsibilities:
- Load and parse `opening.json` via `UnityWebRequest` (required for `StreamingAssets` on all platforms)
- On Start: disable `PlayerController` input, disable all enemy AI, activate `CutsceneCamera`
- `ShowLine(int index)`: update camera targets, call `CrossFade` on speaker's Animator, update `DialogueUI`
- Listen for left mouse click (`Input.GetMouseButtonDown(0)`) to advance
- `EndCutscene()`: re-enable input and AI, swap camera priorities, hide UI

**`DialogueUI.cs`** — Manages the dialogue panel.

Responsibilities:
- `Show(string speakerName, string text)`: set speaker label and body text, make panel visible
- `Hide()`: hide panel
- Coroutine to blink the `[ Click to continue ]` prompt (alpha ping-pong)

### Modified scripts

**`PlayerController.cs`**
- Add `public bool InputEnabled = true`
- Wrap `Update` movement/jump/attack handling in `if (!InputEnabled) return;`

**`PlayerAnimator.cs`**
- Add `public void PlayCutsceneAnim(string stateName)`
- Implementation: `animator.CrossFade(stateName, 0.2f)`

**`EnemyBase.cs`**
- Add `public bool AIEnabled = true`
- Guard `NavMeshAgent.SetDestination` and attack logic with `if (!AIEnabled) return;`

---

## UI Structure

Added under the existing Canvas:

```
DialoguePanel            (Image — semi-transparent black, anchored bottom-center)
  ├── SpeakerName        (TextMeshPro — bold, character name)
  ├── DialogueText       (TextMeshPro — body text)
  └── ClickPrompt        (TextMeshPro — "[ Click to continue ]", blinking)
```

Text is displayed instantly (no typewriter effect). All text is in English (no Chinese font required).

---

## Integration Point

`CutsceneManager.Start()` runs before any gameplay. `SimpleGameManager` does not need modification — its enemy-scan loop simply finds no dead enemies during the cutscene and does nothing.

---

## Out of Scope

- Typewriter / character-by-character text reveal
- Voice audio
- Multiple scenes or chapter transitions
- Skipping the entire cutscene with a single key (can be added later)
