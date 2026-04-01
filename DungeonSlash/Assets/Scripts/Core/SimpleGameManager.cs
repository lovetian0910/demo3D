using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 简化版游戏管理器：不做房间切换，场景里直接放敌人，全部消灭就胜利。
/// </summary>
public class SimpleGameManager : MonoBehaviour
{
    public static SimpleGameManager Instance { get; private set; }

    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private GameObject victoryUI;
    [SerializeField] private float checkEnemyInterval = 0.5f;

    private bool isGameOver;
    private float checkTimer;
    private bool enemiesExisted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (isGameOver) return;

        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = checkEnemyInterval;

        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

        if (enemies.Length > 0)
        {
            enemiesExisted = true;
        }

        if (enemiesExisted && enemies.Length == 0)
        {
            OnAllEnemiesDefeated();
        }
    }

    public void OnPlayerDeath()
    {
        if (isGameOver) return;
        isGameOver = true;

        // 显示光标（Play 模式可能隐藏了光标）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }
    }

    private void OnAllEnemiesDefeated()
    {
        if (isGameOver) return;
        isGameOver = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (victoryUI != null)
        {
            victoryUI.SetActive(true);
        }
    }

    /// <summary>
    /// 由 UI 按钮调用，重新加载场景
    /// </summary>
    public void RestartGame()
    {
        isGameOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
