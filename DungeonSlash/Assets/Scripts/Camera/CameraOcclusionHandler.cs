// ============================================================
// CameraOcclusionHandler.cs — 相机遮挡透明化处理
// ============================================================
//
// 🎓 核心问题：
//   俯视角 3D 游戏中，柱子、墙壁等高大物体可能挡在相机和玩家之间
//   导致玩家看不到自己的角色
//
// 🎓 解决思路：
//   每帧从相机向玩家发射射线（Raycast）
//   射线碰到的物体 = 正在遮挡玩家的物体
//   把这些物体的材质变成半透明
//   射线不再碰到时 = 不再遮挡 → 恢复不透明
//
// 🎓 方案权衡：
//   材质透明化会增加少量 Batches（Opaque→Transparent 切换）
//   但视觉效果好（平滑过渡），俯视角遮挡物通常不多
//   对于本项目规模，这点 Batches 增加可以接受
//
// 使用方法：
//   挂到 Main Camera（或 CinemachineCamera 所在的 GameObject）上
//   设置 Obstacle Layers 为包含墙壁/柱子的层
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public class CameraOcclusionHandler : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("要保护不被遮挡的目标（玩家）")]
    [SerializeField] private Transform target;

    [Header("检测设置")]
    [Tooltip("哪些层的物体算遮挡物")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Tooltip("射线检测的粗细（球形射线半径）")]
    [SerializeField] private float sphereRadius = 0.5f;

    [Header("透明效果")]
    [Tooltip("遮挡时的透明度（0=完全透明，1=完全不透明）")]
    [SerializeField] [Range(0f, 1f)] private float occludedAlpha = 0.3f;

    [Tooltip("透明度变化速度（越大越快）")]
    [SerializeField] private float fadeSpeed = 5f;

    private class OccludedObject
    {
        public Renderer[] renderers;
        public Color[] originalColors;
        public float[] originalSurfaceTypes;
        public Material[] materials;
        public float currentAlpha;
        public bool isOccluding;
    }

    private Dictionary<int, OccludedObject> occludedObjects = new Dictionary<int, OccludedObject>();
    private RaycastHit[] hitResults = new RaycastHit[20];

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("[CameraOcclusionHandler] 找不到 Player，请手动指定 target");
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        foreach (var kvp in occludedObjects)
        {
            kvp.Value.isOccluding = false;
        }

        DetectOcclusion();
        UpdateTransparency();
    }

    private void DetectOcclusion()
    {
        Vector3 cameraPos = transform.position;
        Vector3 targetPos = target.position + Vector3.up * 1f;
        Vector3 direction = targetPos - cameraPos;
        float distance = direction.magnitude;

        int hitCount = Physics.SphereCastNonAlloc(
            cameraPos,
            sphereRadius,
            direction.normalized,
            hitResults,
            distance,
            obstacleLayers
        );

        for (int i = 0; i < hitCount; i++)
        {
            GameObject hitObj = hitResults[i].collider.gameObject;

            if (hitObj.transform == target) continue;
            if (hitObj.CompareTag("Player")) continue;

            int id = hitObj.GetInstanceID();

            if (!occludedObjects.ContainsKey(id))
            {
                OccludedObject occluded = CreateOccludedObject(hitObj);
                if (occluded == null) continue;
                occludedObjects[id] = occluded;
            }

            occludedObjects[id].isOccluding = true;
        }
    }

    private OccludedObject CreateOccludedObject(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return null;

        List<Material> mats = new List<Material>();
        List<Color> colors = new List<Color>();
        List<float> surfaceTypes = new List<float>();

        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                mats.Add(m);

                if (m.HasProperty("_BaseColor"))
                {
                    colors.Add(m.GetColor("_BaseColor"));
                }
                else
                {
                    colors.Add(Color.white);
                }

                if (m.HasProperty("_Surface"))
                {
                    surfaceTypes.Add(m.GetFloat("_Surface"));
                }
                else
                {
                    surfaceTypes.Add(0);
                }
            }
        }

        return new OccludedObject
        {
            renderers = renderers,
            materials = mats.ToArray(),
            originalColors = colors.ToArray(),
            originalSurfaceTypes = surfaceTypes.ToArray(),
            currentAlpha = 1f,
            isOccluding = true
        };
    }

    private void UpdateTransparency()
    {
        List<int> toRemove = new List<int>();

        foreach (var kvp in occludedObjects)
        {
            OccludedObject obj = kvp.Value;

            float targetAlpha = obj.isOccluding ? occludedAlpha : 1f;

            obj.currentAlpha = Mathf.Lerp(obj.currentAlpha, targetAlpha,
                                          Time.deltaTime * fadeSpeed);

            if (!obj.isOccluding && obj.currentAlpha > 0.99f)
            {
                RestoreObject(obj);
                toRemove.Add(kvp.Key);
                continue;
            }

            ApplyTransparency(obj);
        }

        foreach (int id in toRemove)
        {
            occludedObjects.Remove(id);
        }
    }

    private void ApplyTransparency(OccludedObject obj)
    {
        for (int i = 0; i < obj.materials.Length; i++)
        {
            Material mat = obj.materials[i];

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (mat.HasProperty("_BaseColor"))
            {
                Color c = obj.originalColors[i];
                c.a = obj.currentAlpha;
                mat.SetColor("_BaseColor", c);
            }
        }
    }

    private void RestoreObject(OccludedObject obj)
    {
        for (int i = 0; i < obj.materials.Length; i++)
        {
            Material mat = obj.materials[i];

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", obj.originalSurfaceTypes[i]);

                if (obj.originalSurfaceTypes[i] == 0)
                {
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", obj.originalColors[i]);
            }
        }
    }

    private void OnDisable()
    {
        foreach (var kvp in occludedObjects)
        {
            RestoreObject(kvp.Value);
        }
        occludedObjects.Clear();
    }
}
