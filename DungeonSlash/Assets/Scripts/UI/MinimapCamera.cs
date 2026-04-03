// ============================================================
// MinimapCamera.cs — 小地图相机控制器
// ============================================================
// 功能：
//   1. 从正上方用正交投影向下看场景
//   2. 跟随玩家 X/Z 位置（Y 固定在高处）
//   3. 渲染到 RenderTexture → UI RawImage 显示
//
// 🎓 为什么需要第二个相机？
//   主相机是 45° 俯视角透视投影，小地图需要 90° 正下方正交投影
//   两个相机互不干扰，各自渲染到不同的目标：
//     主相机 → 屏幕
//     小地图相机 → RenderTexture → UI
//
// 🎓 正交投影 vs 透视投影
//   透视：近大远小，有真实感（主相机用这个）
//   正交：远近一样大，适合小地图/2D（你在 Cocos/Godot 用的就是正交）
//   对比：Godot 的 Camera2D 默认就是正交的，所以 2D 游戏不用关心这个
//
// 🎓 RenderTexture（渲染纹理）
//   普通相机把画面画到屏幕上
//   设置了 TargetTexture 的相机把画面"画到一张图片上"
//   这张图片可以当材质用（贴到 3D 物体上）或者显示在 UI 上
//   类似 2D 引擎的 "离屏渲染" / "viewport texture"
// ============================================================

using UnityEngine;
using UnityEngine.Rendering.Universal;   // ← URP 相机 API

public class MinimapCamera : MonoBehaviour
{
    [Header("跟随目标")]
    [Tooltip("小地图相机要跟随的目标，通常是玩家")]
    [SerializeField] private Transform target;

    [Header("相机参数")]
    [Tooltip("相机距地面的高度（越高能看到越多，但细节越少）")]
    [SerializeField] private float height = 30f;

    [Tooltip("正交大小 = 视野范围的一半（单位：Unity 世界单位）\n" +
             "值越大，小地图看到的范围越广")]
    [SerializeField] private float orthographicSize = 20f;

    [Header("Renderer 配置")]
    [Tooltip("小地图专用 Renderer 在 URP Asset Renderer List 中的索引\n" +
             "默认 Renderer 是 0，小地图 Renderer 通常是 1")]
    [SerializeField] private int rendererIndex = 1;

    [Header("性能优化")]
    [Tooltip("小地图每隔多少帧渲染一次\n" +
             "1 = 每帧渲染（最流畅但最费）\n" +
             "3 = 每 3 帧渲染一次（推荐，肉眼几乎看不出差别）\n" +
             "6 = 每 6 帧渲染一次（最省，小地图会有轻微延迟感）")]
    [SerializeField][Range(1, 10)] private int renderInterval = 3;

    // 🎓 为什么缓存 Camera 组件？
    // GetComponent 每次调用都要遍历组件列表（虽然 Unity 做了优化）
    // Awake 里缓存一次，Update 里直接用，这是 Unity 性能最佳实践
    // 面试常问：GetComponent 的性能开销，以及 Awake vs Start 的区别
    private Camera cam;
    private int frameCount;

    // ============================================================
    // 🎓 Awake vs Start
    //   Awake：对象实例化时调用（最早），用于自身初始化
    //   Start：所有 Awake 执行完后调用，用于需要引用其他对象的初始化
    //   这里获取自身组件用 Awake，查找玩家用 Start
    //   顺序保证：所有对象的 Awake → 所有对象的 Start
    // ============================================================
    private void Awake()
    {
        cam = GetComponent<Camera>();

        // 🎓 移除多余的 AudioListener
        // Unity 要求场景中只能有 1 个 AudioListener（"耳朵"）
        // 主相机已经有了，小地图相机不需要听声音
        // 如果不移除，Console 会一直报警告：
        //   "There are 2 audio listeners in the scene"
        var listener = GetComponent<AudioListener>();
        if (listener != null)
        {
            Destroy(listener);
        }

        // 🎓 正交投影设置
        // projection = orthographic → 关闭透视效果
        // orthographicSize → 垂直方向能看到多少单位（的一半）
        // 例如 size=20 → 垂直方向看到 40 个单位
        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;

        // 🎓 指定使用哪个 Renderer
        // URP 允许不同相机使用不同的 Renderer Data
        // 主相机用默认 Renderer (index 0)：完整的光照、阴影、后处理
        // 小地图相机用 MinimapRenderer (index 1)：只有 Override Material
        //
        // UniversalAdditionalCameraData 是 URP 给 Camera 加的扩展组件
        // Unity 6 中每个 Camera 自动带有这个组件
        var urpCameraData = cam.GetUniversalAdditionalCameraData();
        urpCameraData.SetRenderer(rendererIndex);

        // ============================================================
        // 🎓 关闭小地图相机的光照/阴影渲染
        // ============================================================
        //
        // 即使 Shader 是 Unlit（Lighting Off），URP 的渲染管线仍然会：
        //   1. 为每个相机做光源剔除（哪些灯影响视野内的物体）
        //   2. 为主光源生成阴影贴图（Shadow Map）
        //   3. 设置光照 Constant Buffer 到 GPU
        //
        // 这些操作会产生额外的 Batches（你去掉光源后 Batches 大降就是这个原因）
        //
        // 🎓 解决方案：通过 URP Camera Data 关闭这些功能
        //
        // renderShadows = false：
        //   告诉 URP "这个相机不需要阴影"
        //   省掉 Shadow Map 的生成 Pass（这通常是最大的开销来源）
        //   每个投射阴影的灯 × 每个 Shadow Cascade = 多个额外 Batch
        //
        // 🎓 面试说法："通过 URP 的多 Renderer 架构，为不同渲染目标
        //   配置独立的光照策略，非关键渲染（小地图）关闭阴影和附加光照，
        //   减少不必要的 Draw Call"
        // ============================================================
        urpCameraData.renderShadows = false;

        // 🎓 关闭后处理（Post Processing）
        // 小地图不需要 Bloom、色调映射等效果
        // 后处理本身也会产生额外的 Blit Pass（全屏复制操作）
        urpCameraData.renderPostProcessing = false;
    }

    private void Start()
    {
        // 如果没在 Inspector 拖拽指定目标，自动找玩家
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("[MinimapCamera] 找不到 Player tag 的物体，" +
                                 "请确保玩家有 Player tag 或手动拖拽 target");
            }
        }
    }

    // ============================================================
    // 🎓 为什么用 LateUpdate 而不是 Update？
    //   Unity 的执行顺序：FixedUpdate → Update → LateUpdate
    //   玩家移动通常在 Update 里执行
    //   如果相机也在 Update 里跟随，可能"跑在玩家前面"（执行顺序不确定）
    //   LateUpdate 保证在所有 Update 之后执行 → 相机永远跟在玩家后面
    //
    //   对比 Godot：类似 _process 之后手动调相机
    //   对比 Cocos：类似 lateUpdate 回调
    //   面试高频题："为什么相机跟随写在 LateUpdate？"
    // ============================================================
    private void LateUpdate()
    {
        if (target == null) return;

        // 🎓 只跟随 X/Z，Y 固定
        // 小地图相机始终在玩家正上方，不随地形高低变化
        // 如果地图有上下层，可能需要更复杂的逻辑
        Vector3 newPos = target.position;
        newPos.y = height;
        transform.position = newPos;

        // 🎓 朝下看 = rotation (90, 0, 0)
        // Quaternion.Euler 把欧拉角转成四元数
        // x=90 表示绕 X 轴旋转 90°，刚好从上往下看
        // 四元数（Quaternion）是 3D 旋转的标准表示，避免万向锁问题
        // 面试常问：为什么用四元数而不是欧拉角？
        //   → 欧拉角有万向锁（Gimbal Lock），四元数没有
        //   → 四元数插值更平滑（Slerp）
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // ============================================================
        // 🎓 降频渲染优化
        //   小地图不需要每帧刷新 —— 它只显示大致位置，
        //   玩家不会盯着小地图看每一帧的变化。
        //
        //   原理：camera.enabled = false 时相机不渲染（0 batches）
        //   只在指定帧开启，渲染一次后立即关闭
        //
        //   renderInterval = 3 时：
        //     帧 1: 关  帧 2: 关  帧 3: 开  帧 4: 关  帧 5: 关  帧 6: 开 ...
        //     小地图的 batches 只在 1/3 的帧出现
        //     60fps 下相当于小地图以 20fps 刷新，完全够用
        //
        //   🎓 为什么 RenderTexture 关了相机画面不会消失？
        //   RenderTexture 是 GPU 内存中的一块画布，它会保留上一次渲染的内容。
        //   相机关了只是"不再画新的"，旧画面依然显示在 RawImage 上。
        //   这和屏幕渲染不同 —— 屏幕每帧都会被清空重画。
        //
        //   这个技巧在 AAA 游戏中很常见：
        //     - 小地图、后视镜：降频渲染
        //     - 反射探针：隔帧更新
        //     - 阴影贴图：远处的阴影降低刷新率
        //   面试说法："通过降低非关键渲染目标的刷新率来减少 GPU 开销"
        // ============================================================
        frameCount++;
        cam.enabled = (frameCount % renderInterval == 0);
    }

    // ============================================================
    // 🎓 OnValidate：编辑器中修改参数时实时预览
    //   只在编辑器模式下调用，Build 后不会执行
    //   当你在 Inspector 拖动 orthographicSize 滑条时
    //   Scene 窗口能实时看到相机范围变化
    //   这是提升开发体验的小技巧
    // ============================================================
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (cam != null)
        {
            cam.orthographicSize = orthographicSize;
        }
    }
#endif
}
