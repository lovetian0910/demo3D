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

    /// <summary>
    /// 🎓 静态标记：记录开场动画是否已在本次游戏进程中播放过。
    /// static 字段属于类本身，不属于 MonoBehaviour 实例——场景重载会销毁
    /// 并重建所有 GameObject，但 static 字段的值在进程生命周期内一直保留。
    /// 因此 Restart（SceneManager.LoadScene）后此值仍为 true，跳过开场动画。
    ///
    /// 🎓 RuntimeInitializeOnLoadMethod 确保每次进入 Play Mode 时重置，
    /// 解决 Unity Editor 中 static 字段不随 Play/Stop 重置的问题。
    /// </summary>
    private static bool hasPlayedOpeningCutscene = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticState()
    {
        hasPlayedOpeningCutscene = false;
    }

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
    private readonly Dictionary<string, GameObject> speakerMap = new();

    private List<DialogueLine> lines = new();
    private int currentLineIndex = -1;
    private bool waitingForClick = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (dialogueUI == null || cutsceneCamera == null || followCamera == null)
        {
            Debug.LogError("[CutsceneManager] Required references not set in Inspector. Disabling.");
            enabled = false;
            return;
        }

        // Find player components
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerAnimator   = player.GetComponent<PlayerAnimator>();
        }

        // Build stable speaker map for enemies
        EnemyBase[] foundEnemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        for (int i = 0; i < foundEnemies.Length; i++)
            speakerMap[$"enemy_{i}"] = foundEnemies[i].gameObject;

        // 🎓 已播过就跳过，static 标记在 Restart（SceneManager.LoadScene）后依然保持 true。
        // 跳过时需恢复摄像机为游戏状态，否则 Inspector 默认值可能让 cutsceneCamera 抢占。
        if (hasPlayedOpeningCutscene)
        {
            RestoreCameraToGameplay();
            return;
        }

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
        // 🎓 On macOS/Windows Editor, streamingAssetsPath is a plain file path.
        // UnityWebRequest needs an explicit file:// prefix to treat it as a local URL.
        // On Android (jar://) and WebGL (http://), streamingAssetsPath already includes
        // the correct protocol, so we only prepend file:// when it's missing.
        string rawPath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        string path = rawPath.StartsWith("http") || rawPath.StartsWith("jar:")
            ? rawPath
            : "file://" + rawPath;
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
        hasPlayedOpeningCutscene = true;  // 标记已播，Restart 后不再重播

        // Freeze player input
        if (playerController != null) playerController.InputEnabled = false;

        // Freeze all enemy AI
        foreach (var kvp in speakerMap)
        {
            var eb = kvp.Value.GetComponent<EnemyBase>();
            if (eb != null) eb.AIEnabled = false;
        }

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

            // 🎓 Face the speaker toward where the CutsceneCamera will be.
            // Follow Offset (0, 1.5, 2) means the camera sits 2 units in front
            // of the character in world +Z. We want the character to face -Z (toward
            // the camera), so we apply the inverse of the offset's XZ direction.
            // This is calculated directly without waiting for Cinemachine to update.
            // Camera is at +Z offset from character, so character must face +Z toward camera
            Vector3 camOffset = new Vector3(0f, 0f, 2f); // must match Follow Offset Z
            Vector3 toCameraDir = camOffset;
            toCameraDir.y = 0f;
            if (toCameraDir != Vector3.zero)
                speakerGO.transform.rotation = Quaternion.LookRotation(toCameraDir);
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
        waitingForClick = false;
        StartCoroutine(RestoreInputNextFrame());

        // Restore enemy AI
        foreach (var kvp in speakerMap)
        {
            var eb = kvp.Value.GetComponent<EnemyBase>();
            if (eb != null) eb.AIEnabled = true;
        }

        // Return camera to follow view
        RestoreCameraToGameplay();

        // Hide UI
        dialogueUI.Hide();

        // Return player to Idle
        if (playerAnimator != null) playerAnimator.PlayCutsceneAnim("Idle");
    }

    /// <summary>
    /// 🎓 同帧问题（Same-frame Input Bug）：
    /// EndCutscene 和 PlayerCombat.Update 都在同一帧执行。
    /// 如果在同一帧里把 InputEnabled 改回 true，PlayerCombat 会立刻
    /// 读到那次 GetMouseButtonDown(0)，触发攻击。
    /// 延迟一帧恢复输入，让那次点击事件在下一帧前消耗掉。
    /// </summary>
    private IEnumerator RestoreInputNextFrame()
    {
        yield return null; // wait one frame
        if (playerController != null) playerController.InputEnabled = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// 将摄像机优先级恢复为游戏状态（FollowCamera 生效，CutsceneCamera 退出）。
    /// 在两处调用：① 跳过开场动画时，② 开场动画正常结束时。
    /// </summary>
    private void RestoreCameraToGameplay()
    {
        cutsceneCamera.Priority = 0;
        followCamera.Priority   = 10;
    }

    private GameObject ResolveSpeaker(string speaker)
    {
        if (speaker == "player")
            return playerController != null ? playerController.gameObject : null;

        if (speakerMap.TryGetValue(speaker, out GameObject mapped))
            return mapped;

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
