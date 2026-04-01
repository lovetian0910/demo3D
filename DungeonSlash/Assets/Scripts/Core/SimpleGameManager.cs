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
    [SerializeField] private float checkEnemyInterval = 0.5f; // 每 0.5 秒检查一次，不用每帧检查

    private bool isGameOver;
    private float checkTimer;
    private bool enemiesExisted; // 确保场景中确实生成过敌人

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

        // 查找场景中所有存活的敌人
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

        if (enemies.Length > 0)
        {
            enemiesExisted = true;
        }

        // 所有敌人都死了且之前确实有过敌人
        if (enemiesExisted && enemies.Length == 0)
        {
            OnAllEnemiesDefeated();
        }
    }

    public void OnPlayerDeath()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }

        Invoke(nameof(EnableRestart), 2f);
    }

    private void OnAllEnemiesDefeated()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (victoryUI != null)
        {
            victoryUI.SetActive(true);
        }

        Invoke(nameof(EnableRestart), 2f);
    }

    private void EnableRestart()
    {
        StartCoroutine(WaitForRestart());
    }

    private System.Collections.IEnumerator WaitForRestart()
    {
        while (!Input.anyKeyDown)
        {
            yield return null;
        }
        isGameOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
