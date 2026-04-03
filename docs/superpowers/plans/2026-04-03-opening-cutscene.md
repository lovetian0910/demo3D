# Opening Cutscene System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opening cutscene that freezes gameplay, cuts the camera to close-up shots of each speaking character, shows narrative text, and returns to gameplay after the player clicks through all lines.

**Architecture:** A `CutsceneManager` singleton reads `StreamingAssets/dialogue/opening.json`, disables player input and enemy AI, then drives camera targets and animations line-by-line. A separate `DialogueUI` component owns the UI panel. Camera switching is handled by swapping Cinemachine Camera priorities.

**Tech Stack:** Unity 6, C#, Cinemachine v4, TextMeshPro, UnityWebRequest (StreamingAssets), Animator.CrossFade

---

## File Map

| Action | File |
|--------|------|
| **Create** | `Assets/StreamingAssets/dialogue/opening.json` |
| **Create** | `Assets/Scripts/Cutscene/DialogueLine.cs` |
| **Create** | `Assets/Scripts/Cutscene/DialogueUI.cs` |
| **Create** | `Assets/Scripts/Cutscene/CutsceneManager.cs` |
| **Modify** | `Assets/Scripts/Player/PlayerController.cs` |
| **Modify** | `Assets/Scripts/Player/PlayerAnimator.cs` |
| **Modify** | `Assets/Scripts/Enemy/EnemyBase.cs` |

Scene work (done in Unity Editor, not via script):
- Add `CutsceneCamera` GameObject with Cinemachine Camera component
- Add `DialoguePanel` UI hierarchy under Canvas
- Wire references on `CutsceneManager` in Inspector

---

## Task 1: Add `InputEnabled` to PlayerController

**Files:**
- Modify: `Assets/Scripts/Player/PlayerController.cs`

- [ ] **Step 1: Add the flag and guard the Update body**

Open `PlayerController.cs`. Add one public field and one early-return guard so that when `InputEnabled` is false, no movement, jumping, or attack input is read. The fall-death check and gravity must still run (player should not float during cutscene).

Replace the `Update` method and add the field so the file reads:

```csharp
public bool InputEnabled = true;

private void Update()
{
    if (playerHealth != null && playerHealth.IsDead) return;

    if (transform.position.y < fallDeathY)
    {
        if (playerHealth != null && !playerHealth.IsDead)
            playerHealth.TakeDamage(9999f, Vector3.zero);
        return;
    }

    if (!InputEnabled)
    {
        ApplyGravity();
        return;
    }

    HandleMovement();
    HandleJump();
    ApplyGravity();
}
```

- [ ] **Step 2: Verify in Editor**

Enter Play mode. The game should start and play normally. No compile errors.

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Player/PlayerController.cs
git commit -m "feat: add InputEnabled flag to PlayerController for cutscene support"
```

---

## Task 2: Add `PlayCutsceneAnim` to PlayerAnimator

**Files:**
- Modify: `Assets/Scripts/Player/PlayerAnimator.cs`

- [ ] **Step 1: Add the method**

Add this method at the bottom of the class, before the closing `}`:

```csharp
/// <summary>
/// 🎓 CrossFade vs SetTrigger:
/// CrossFade can jump directly to any named State by string, regardless of
/// Animator graph transitions. Ideal for data-driven cutscene animations where
/// the state name comes from JSON. Transition duration 0.2f blends smoothly.
/// </summary>
public void PlayCutsceneAnim(string stateName)
{
    animator.CrossFade(stateName, 0.2f);
}
```

- [ ] **Step 2: Verify in Editor**

Enter Play mode. No compile errors. Existing animations (attack, jump, hit) still work.

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Player/PlayerAnimator.cs
git commit -m "feat: add PlayCutsceneAnim to PlayerAnimator using CrossFade"
```

---

## Task 3: Add `AIEnabled` to EnemyBase

**Files:**
- Modify: `Assets/Scripts/Enemy/EnemyBase.cs`

- [ ] **Step 1: Add the flag and guard Update**

Add one public field. Guard the `Update` method body so enemies freeze when `AIEnabled` is false. The `isDead` check must remain first.

Add the field after the existing private fields (around line 56), and replace the `Update` method:

```csharp
public bool AIEnabled = true;
```

```csharp
protected virtual void Update()
{
    if (isDead || playerTransform == null) return;
    if (!AIEnabled)
    {
        agent.isStopped = true;
        return;
    }

    attackTimer -= Time.deltaTime;
    float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

    switch (currentState)
    {
        case EnemyState.Idle:   UpdateIdle(distToPlayer);   break;
        case EnemyState.Chase:  UpdateChase(distToPlayer);  break;
        case EnemyState.Attack: UpdateAttack(distToPlayer); break;
        case EnemyState.Hit:    UpdateHit();                break;
    }

    if (animator != null)
        animator.SetFloat(SpeedHash, agent.velocity.magnitude);
}
```

- [ ] **Step 2: Verify in Editor**

Enter Play mode. Enemies still chase and attack normally. No compile errors.

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Enemy/EnemyBase.cs
git commit -m "feat: add AIEnabled flag to EnemyBase for cutscene freeze"
```

---

## Task 4: Create dialogue data types and JSON file

**Files:**
- Create: `Assets/Scripts/Cutscene/DialogueLine.cs`
- Create: `Assets/StreamingAssets/dialogue/opening.json`

- [ ] **Step 1: Create the data class**

Create `Assets/Scripts/Cutscene/DialogueLine.cs`:

```csharp
using System;
using System.Collections.Generic;

/// <summary>
/// Mirrors the JSON schema for one line of opening dialogue.
/// speaker: "player" | "enemy_0" | "enemy_1" | "enemy_2"
/// animation: Animator State name (e.g. "Relax", "Nod Head", "Clapping")
/// </summary>
[Serializable]
public class DialogueLine
{
    public string speaker;
    public string text;
    public string animation;
}

[Serializable]
public class DialogueData
{
    public List<DialogueLine> lines;
}
```

- [ ] **Step 2: Create the StreamingAssets folder and JSON file**

Create the folder `Assets/StreamingAssets/dialogue/` and the file `opening.json`:

```json
{
  "lines": [
    { "speaker": "player",  "text": "Darkness has consumed the dungeon...",   "animation": "Relax"              },
    { "speaker": "player",  "text": "I alone must descend into this abyss.",  "animation": "Nod Head"           },
    { "speaker": "enemy_0", "text": "Another fool walks to their death.",     "animation": "Clapping"           },
    { "speaker": "enemy_0", "text": "We will crush you.",                     "animation": "Shake Head"         }
  ]
}
```

- [ ] **Step 3: Verify in Editor**

Unity should import the script with no errors. In Project window, confirm `Assets/StreamingAssets/dialogue/opening.json` exists.

- [ ] **Step 4: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Cutscene/
git add "DungeonSlash/Assets/StreamingAssets/"
git commit -m "feat: add DialogueLine data class and opening.json cutscene data"
```

---

## Task 5: Create DialogueUI script

**Files:**
- Create: `Assets/Scripts/Cutscene/DialogueUI.cs`

- [ ] **Step 1: Create the script**

Create `Assets/Scripts/Cutscene/DialogueUI.cs`:

```csharp
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Owns the dialogue panel: speaker name, body text, and blinking click-prompt.
/// Wire references in Inspector after creating the UI hierarchy in Task 6.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI clickPromptText;

    private Coroutine blinkCoroutine;

    public void Show(string speakerName, string text)
    {
        panel.SetActive(true);
        speakerNameText.text = speakerName;
        dialogueText.text = text;

        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkPrompt());
    }

    public void Hide()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        panel.SetActive(false);
    }

    private IEnumerator BlinkPrompt()
    {
        clickPromptText.text = "[ Click to continue ]";
        while (true)
        {
            float alpha = Mathf.PingPong(Time.time * 2f, 1f);
            Color c = clickPromptText.color;
            c.a = alpha;
            clickPromptText.color = c;
            yield return null;
        }
    }
}
```

- [ ] **Step 2: Verify in Editor**

No compile errors. Script appears in Project window.

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Cutscene/DialogueUI.cs
git commit -m "feat: add DialogueUI component for cutscene dialogue panel"
```

---

## Task 6: Build the DialoguePanel UI hierarchy in the scene

This task is done entirely in the Unity Editor (no code).

- [ ] **Step 1: Create DialoguePanel under Canvas**

In the Hierarchy, right-click the existing **Canvas** → UI → Image. Rename it `DialoguePanel`.

In Inspector:
- Anchor: bottom-center (`Alt+click` the bottom-center anchor preset)
- Pivot: `(0.5, 0)`
- Pos X: `0`, Pos Y: `20`
- Width: `900`, Height: `180`
- Color: `(0, 0, 0, 0.75)` (semi-transparent black)

- [ ] **Step 2: Add SpeakerName text**

Right-click `DialoguePanel` → UI → Text - TextMeshPro. Rename it `SpeakerName`.
- Anchor: top-left
- Pos X: `20`, Pos Y: `-15`
- Width: `400`, Height: `40`
- Font Style: Bold
- Font Size: `20`
- Text: `Hero` (placeholder, overwritten at runtime)

- [ ] **Step 3: Add DialogueText**

Right-click `DialoguePanel` → UI → Text - TextMeshPro. Rename it `DialogueText`.
- Anchor: top-left
- Pos X: `20`, Pos Y: `-60`
- Width: `860`, Height: `70`
- Font Size: `18`
- Text: `Dialogue goes here.` (placeholder)

- [ ] **Step 4: Add ClickPrompt**

Right-click `DialoguePanel` → UI → Text - TextMeshPro. Rename it `ClickPrompt`.
- Anchor: bottom-right
- Pos X: `-20`, Pos Y: `15`
- Width: `260`, Height: `30`
- Font Size: `14`
- Color: `(1, 1, 1, 1)` (white, alpha animated at runtime)
- Text: `[ Click to continue ]`

- [ ] **Step 5: Disable the panel**

Select `DialoguePanel` in Hierarchy. In Inspector, uncheck the active checkbox (top-left of Inspector). It should be hidden at game start.

- [ ] **Step 6: Commit scene**

```bash
git add DungeonSlash/Assets/Scenes/
git commit -m "feat: add DialoguePanel UI hierarchy to Main scene"
```

---

## Task 7: Add CutsceneCamera to the scene

This task is done in the Unity Editor.

- [ ] **Step 1: Create the Cinemachine Camera**

In Hierarchy, go to **GameObject → Cinemachine → Targeted Cameras → Follow Camera**. Rename the new object `CutsceneCamera`.

- [ ] **Step 2: Configure CutsceneCamera**

Select `CutsceneCamera`. In Inspector:
- **Priority:** `10`
- **Follow:** leave empty for now (set at runtime by CutsceneManager)
- **Look At:** leave empty for now

Expand **Position Control** (or the Composer/Body section depending on your Cinemachine v4 layout):
- Set **Follow Offset** to `(0, 1.5, -2)`

Set the camera's rotation to approximately `-10` on the X axis (tilt down slightly toward character's face).

- [ ] **Step 3: Configure FollowCamera priority**

Select the existing `FollowCamera` (or whatever the overhead cam is named). Set its **Priority** to `0`. It will be raised to `10` when the cutscene ends.

- [ ] **Step 4: Configure Cinemachine Brain blend**

Select the **Main Camera** GameObject. Find the **Cinemachine Brain** component.
- Set **Default Blend** to `EaseInOut`, duration `0.3`

- [ ] **Step 5: Commit scene**

```bash
git add DungeonSlash/Assets/Scenes/
git commit -m "feat: add CutsceneCamera and configure Cinemachine blend in Main scene"
```

---

## Task 8: Create CutsceneManager script

**Files:**
- Create: `Assets/Scripts/Cutscene/CutsceneManager.cs`

- [ ] **Step 1: Create the script**

Create `Assets/Scripts/Cutscene/CutsceneManager.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

/// <summary>
/// Drives the opening cutscene: loads JSON, freezes gameplay, advances lines on click.
///
/// 🎓 UnityWebRequest for StreamingAssets:
/// On Android the APK is a zip; File.ReadAllText fails. On WebGL there is no
/// filesystem. UnityWebRequest works on all platforms by using the right
/// protocol internally (file://, jar://, http://).
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera cutsceneCamera;
    [SerializeField] private CinemachineCamera followCamera;

    [Header("UI")]
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Data")]
    [SerializeField] private string jsonFileName = "dialogue/opening.json";

    // Resolved at runtime
    private PlayerController playerController;
    private PlayerAnimator playerAnimator;
    private List<EnemyBase> enemies = new();

    private List<DialogueLine> lines = new();
    private int currentLineIndex = -1;
    private bool waitingForClick = false;
    private bool cutsceneActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Find player components
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerAnimator   = player.GetComponent<PlayerAnimator>();
        }

        // Find all enemies
        foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            enemies.Add(e);

        StartCoroutine(LoadAndPlay());
    }

    private void Update()
    {
        if (!waitingForClick) return;
        if (Input.GetMouseButtonDown(0))
        {
            waitingForClick = false;
            AdvanceLine();
        }
    }

    // ── Loading ────────────────────────────────────────────────────────────

    private IEnumerator LoadAndPlay()
    {
        string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        using var req = UnityWebRequest.Get(path);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[CutsceneManager] Could not load {path}: {req.error}. Skipping cutscene.");
            yield break;
        }

        DialogueData data = JsonUtility.FromJson<DialogueData>(req.downloadHandler.text);
        if (data == null || data.lines == null || data.lines.Count == 0)
        {
            Debug.LogWarning("[CutsceneManager] opening.json is empty or malformed. Skipping cutscene.");
            yield break;
        }

        lines = data.lines;
        BeginCutscene();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void BeginCutscene()
    {
        cutsceneActive = true;

        // Freeze player input
        if (playerController != null) playerController.InputEnabled = false;

        // Freeze all enemy AI
        foreach (var e in enemies) e.AIEnabled = false;

        // Activate cutscene camera
        cutsceneCamera.Priority = 10;
        followCamera.Priority   = 0;

        currentLineIndex = -1;
        AdvanceLine();
    }

    private void AdvanceLine()
    {
        currentLineIndex++;

        if (currentLineIndex >= lines.Count)
        {
            EndCutscene();
            return;
        }

        ShowLine(lines[currentLineIndex]);
    }

    private void ShowLine(DialogueLine line)
    {
        // Resolve speaker GameObject
        GameObject speakerGO = ResolveSpeaker(line.speaker);
        string speakerLabel  = FormatSpeakerLabel(line.speaker);

        // Point CutsceneCamera at this speaker
        if (speakerGO != null)
        {
            cutsceneCamera.Follow  = speakerGO.transform;
            cutsceneCamera.LookAt  = speakerGO.transform;
        }

        // Play animation on speaker
        Animator anim = speakerGO != null
            ? speakerGO.GetComponentInChildren<Animator>()
            : null;

        if (anim != null && !string.IsNullOrEmpty(line.animation))
            anim.CrossFade(line.animation, 0.2f);

        // Show dialogue UI
        dialogueUI.Show(speakerLabel, line.text);

        waitingForClick = true;
    }

    private void EndCutscene()
    {
        cutsceneActive = false;
        waitingForClick = false;

        // Restore player input
        if (playerController != null) playerController.InputEnabled = true;

        // Restore enemy AI
        foreach (var e in enemies) e.AIEnabled = true;

        // Return camera to follow view
        cutsceneCamera.Priority = 0;
        followCamera.Priority   = 10;

        // Hide UI
        dialogueUI.Hide();

        // Return player to Idle
        if (playerAnimator != null) playerAnimator.PlayCutsceneAnim("Idle");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private GameObject ResolveSpeaker(string speaker)
    {
        if (speaker == "player")
            return GameObject.FindGameObjectWithTag("Player");

        if (speaker.StartsWith("enemy_"))
        {
            if (int.TryParse(speaker.Substring(6), out int idx))
            {
                GameObject[] enemyGOs = GameObject.FindGameObjectsWithTag("Enemy");
                if (idx < enemyGOs.Length) return enemyGOs[idx];
            }
        }

        Debug.LogWarning($"[CutsceneManager] Unknown speaker '{speaker}'");
        return null;
    }

    private static string FormatSpeakerLabel(string speaker)
    {
        if (speaker == "player") return "Hero";
        if (speaker.StartsWith("enemy_")) return "Enemy";
        return speaker;
    }
}
```

- [ ] **Step 2: Verify in Editor**

No compile errors. The script uses `Unity.Cinemachine` — confirm the namespace resolves (Cinemachine v4 uses `Unity.Cinemachine`, not `Cinemachine`).

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Cutscene/CutsceneManager.cs
git commit -m "feat: add CutsceneManager singleton for opening cutscene"
```

---

## Task 9: Wire everything in the scene

This task is done in the Unity Editor.

- [ ] **Step 1: Create CutsceneManager GameObject**

In Hierarchy, right-click → Create Empty. Rename it `CutsceneManager`.

Drag `CutsceneManager.cs` onto it. Also drag `DialogueUI.cs` onto the `DialoguePanel` GameObject.

- [ ] **Step 2: Wire DialogueUI references**

Select `DialoguePanel` in Hierarchy. In the `DialogueUI` component:
- **Panel** → drag `DialoguePanel` itself
- **Speaker Name Text** → drag `SpeakerName`
- **Dialogue Text** → drag `DialogueText`
- **Click Prompt Text** → drag `ClickPrompt`

- [ ] **Step 3: Wire CutsceneManager references**

Select `CutsceneManager` in Hierarchy. In the `CutsceneManager` component:
- **Cutscene Camera** → drag `CutsceneCamera`
- **Follow Camera** → drag the existing overhead Cinemachine camera
- **Dialogue UI** → drag `DialoguePanel` (which has the `DialogueUI` component)
- **Json File Name** → `dialogue/opening.json` (default, no change needed)

- [ ] **Step 4: Test full cutscene flow**

Enter Play mode. Expected sequence:
1. Player and enemies appear but are frozen
2. Camera shows Player close-up; Player plays `Relax` animation
3. "Darkness has consumed the dungeon..." appears in the dialogue panel
4. Click mouse → Player plays `Nod Head`, text updates
5. Click mouse → Camera blends to `enemy_0`; enemy plays `Clapping`
6. Click mouse → enemy plays `Shake Head`, text updates
7. Click mouse → UI hides, camera returns to overhead, enemies start chasing

- [ ] **Step 5: Commit scene**

```bash
git add DungeonSlash/Assets/Scenes/
git commit -m "feat: wire CutsceneManager and DialogueUI references in Main scene"
```

---

## Task 10: Add cutscene animation States to Animator Controllers

The animations (`Relax`, `Nod Head`, `Clapping`, `Shake Head`) must exist as States in the Animator Controller, otherwise `CrossFade` silently does nothing.

This task is done in the Unity Editor.

- [ ] **Step 1: Open PlayerAnimator controller**

In Project window, navigate to `Assets/Animations/PlayerAnimator.controller`. Double-click to open the Animator window.

- [ ] **Step 2: Add Player cutscene states**

For each animation below, right-click in the Animator window → **Create State → Empty**. Then in Inspector set **Motion** to the corresponding clip from `ThirdParty/Little Heroes Mega Pack/Animations/`:

| State Name | Motion clip |
|------------|-------------|
| `Relax`    | `Base@Relax` |
| `Nod Head` | `Base@Nod Head` |
| `Defend`   | `Base@Defend` |

These states need no transitions — `CrossFade` bypasses the transition graph.

- [ ] **Step 3: Open MeleeEnemyAnimator controller**

Navigate to `Assets/Animations/MeleeEnemyAnimator.controller`. Open in Animator window.

- [ ] **Step 4: Add Enemy cutscene states**

| State Name | Motion clip |
|------------|-------------|
| `Clapping` | `Base@Clapping` |
| `Shake Head` | `Base@Shake Head` |

- [ ] **Step 5: Test animations**

Enter Play mode. Verify that each character plays the correct animation at each dialogue line. If an animation doesn't play, check the State name matches exactly (case-sensitive) what's in `opening.json`.

- [ ] **Step 6: Commit**

```bash
git add DungeonSlash/Assets/Animations/
git commit -m "feat: add cutscene animation states to PlayerAnimator and MeleeEnemyAnimator"
```

---

## Self-Review

**Spec coverage check:**
- ✅ JSON data layer with speaker/text/animation schema → Task 4
- ✅ UnityWebRequest for StreamingAssets → Task 8
- ✅ PlayerController.InputEnabled → Task 1
- ✅ EnemyBase.AIEnabled → Task 3
- ✅ PlayerAnimator.PlayCutsceneAnim → Task 2
- ✅ CutsceneCamera added, priority switching → Tasks 7 + 8
- ✅ Cinemachine Brain blend configured → Task 7
- ✅ DialogueUI with blink prompt → Task 5 + 6
- ✅ Left-click to advance → Task 8 (Update loop)
- ✅ EndCutscene restores everything → Task 8
- ✅ All text English only → Task 4 (JSON content)
- ✅ Animations added to Animator Controllers → Task 10

**Placeholder scan:** No TBD, no "similar to task N", all code blocks complete.

**Type consistency:**
- `PlayCutsceneAnim(string stateName)` defined in Task 2, called in Task 8 ✅
- `InputEnabled` defined in Task 1, set in Task 8 ✅
- `AIEnabled` defined in Task 3, set in Task 8 ✅
- `DialogueUI.Show(string, string)` / `.Hide()` defined in Task 5, called in Task 8 ✅
- `CinemachineCamera` used throughout — correct Cinemachine v4 type ✅
