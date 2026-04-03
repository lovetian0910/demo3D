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
            anim.CrossFade(line.animation, 0.2f, 0);

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
