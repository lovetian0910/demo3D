using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private GameObject victoryUI;

    private bool isGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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

    public void OnAllRoomsCleared()
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
