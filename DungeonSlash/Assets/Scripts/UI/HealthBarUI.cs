using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    private PlayerHealth playerHealth;

    private void Start()
    {
        playerHealth = GameObject.FindGameObjectWithTag("Player")
            ?.GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (playerHealth == null) return;
        fillImage.fillAmount = playerHealth.HealthPercent;

        fillImage.color = playerHealth.HealthPercent > 0.3f
            ? Color.green
            : Color.red;
    }
}
