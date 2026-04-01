using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);
    private EnemyBase enemy;
    private Camera mainCamera;
    private Canvas canvas;

    private void Awake()
    {
        enemy = GetComponentInParent<EnemyBase>();
        mainCamera = Camera.main;
        canvas = GetComponent<Canvas>();

        if (canvas != null)
        {
            canvas.worldCamera = mainCamera;
        }
    }

    private void LateUpdate()
    {
        if (enemy == null) return;

        transform.position = enemy.transform.position + offset;
        transform.forward = mainCamera.transform.forward;

        fillImage.fillAmount = enemy.HealthPercent;
        canvas.enabled = enemy.HealthPercent < 1f;
    }
}
