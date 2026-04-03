// ============================================================
// DebugStatsUI.cs — 调试信息面板（FPS、Batches 等）
// ============================================================
//
// 🎓 为什么需要这个？
//   Unity 编辑器里有 Stats 面板可以看 FPS 和 Batches
//   但构建出来的版本（WebGL、手机）没有 Stats 面板
//   需要自己写一个 UI 来显示这些信息
//
// 🎓 OnGUI vs Canvas UI
//   这里用的是 OnGUI（Unity 的即时模式 GUI）
//   优点：不需要创建任何 Canvas/GameObject，纯代码搞定
//   缺点：性能比 Canvas UI 差，不适合正式游戏 UI
//   用途：调试工具、开发期信息展示
//   正式项目通常用 Canvas + TextMeshPro
//
// 🎓 对比 2D 引擎：
//   Cocos：类似 cc.debug.setDisplayStats(true)
//   Godot：类似 Performance.get_monitor()
//   Unity 没有内置的一行开关，需要自己写
//
// 使用方法：
//   1. 把脚本挂到场景里任意 GameObject 上（建议挂到 Main Camera）
//   2. 运行时按 F1 开关显示
//   3. Development Build 下自动显示，Release Build 下默认隐藏
// ============================================================

using UnityEngine;
using UnityEngine.Rendering;

public class DebugStatsUI : MonoBehaviour
{
    [Header("显示设置")]
    [Tooltip("是否默认显示（运行时按 F1 切换）")]
    [SerializeField] private bool showOnStart = true;

    [Tooltip("字体大小")]
    [SerializeField] private int fontSize = 20;

    // ---- FPS 计算相关 ----
    // 🎓 FPS 的计算原理：
    //   不是每帧都更新显示（数字会跳来跳去看不清）
    //   而是每隔一段时间（updateInterval）统计一次
    //   在这段时间内数帧数，然后：FPS = 帧数 / 经过的时间
    private float deltaTime;         // 平滑后的帧间隔
    private float fps;               // 当前 FPS
    private int frameCount;          // 统计区间内的帧数
    private float elapsed;           // 统计区间已过的时间
    private float updateInterval = 0.5f;  // 每 0.5 秒更新一次显示

    // ---- Batches 相关 ----
    // 🎓 FrameTimingManager：Unity 提供的性能数据接口
    //   可以获取 GPU 时间、CPU 时间等
    //   但 Batches 数据在构建版本中获取方式有限
    //
    // 🎓 OnFrameRenderComplete：URP 的回调
    //   每帧渲染完成后调用，但构建版本中不提供 Batches 数据
    //   所以我们用 FrameTimingManager 获取帧时间作为替代

    private bool isVisible;
    private GUIStyle boxStyle;
    private GUIStyle textStyle;

    private void Awake()
    {
        // 🎓 Debug.isDebugBuild：判断是否是 Development Build
        //   true  → Development Build（勾了 Development Build）
        //   false → Release Build
        //   Release 下默认隐藏调试信息，避免影响玩家体验
        isVisible = showOnStart && Debug.isDebugBuild;
    }

    private void Update()
    {
        // ---- 计算 FPS ----
        // 🎓 Time.unscaledDeltaTime vs Time.deltaTime
        //   deltaTime 受 Time.timeScale 影响（暂停时为 0）
        //   unscaledDeltaTime 不受影响 → 暂停时也能正确计算 FPS
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        frameCount++;
        elapsed += Time.unscaledDeltaTime;

        if (elapsed >= updateInterval)
        {
            fps = frameCount / elapsed;
            frameCount = 0;
            elapsed = 0f;
        }

        // ---- F1 开关显示 ----
        // 🎓 WebGL 下键盘输入一样能用
        //   浏览器会把键盘事件传给 Unity 的 Canvas
        if (Input.GetKeyDown(KeyCode.F1))
        {
            isVisible = !isVisible;
        }
    }

    // ============================================================
    // 🎓 OnGUI —— 即时模式 GUI
    //   每帧可能被调用多次（Layout 事件 + Repaint 事件）
    //   不要在这里做逻辑计算，只做绘制
    //
    //   对比：
    //     Cocos 的 cc.director.on(cc.Director.EVENT_AFTER_DRAW)
    //     Godot 的 _draw() 函数
    //   Unity 的 OnGUI 是最接近的等价物
    // ============================================================
    private void OnGUI()
    {
        if (!isVisible) return;

        // 初始化样式（只在第一次创建）
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.7f));

            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = fontSize;
            textStyle.normal.textColor = Color.white;
            textStyle.richText = true;     // 🎓 支持 <color> 标签
        }

        // ---- 构建显示文本 ----
        float ms = deltaTime * 1000f;      // 帧间隔（毫秒）

        // 🎓 根据 FPS 值动态变色
        //   60+ → 绿色（流畅）
        //   30-60 → 黄色（可接受）
        //   30 以下 → 红色（卡顿）
        string fpsColor = fps >= 55 ? "#00FF00" :    // 绿
                           fps >= 30 ? "#FFFF00" :    // 黄
                                       "#FF0000";     // 红

        string text = $"<color={fpsColor}>FPS: {fps:F1}</color>\n" +
                       $"Frame: {ms:F1} ms\n" +
                       $"Screen: {Screen.width}×{Screen.height}\n" +
                       $"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}";

        // 🎓 平台特定信息
        // SystemInfo 包含硬件信息，在 WebGL 下也能用
        // 面试时可以提到："通过 SystemInfo 做运行时性能分级"
#if UNITY_WEBGL
        text += $"\nPlatform: WebGL";
#endif
        text += $"\nGPU: {SystemInfo.graphicsDeviceName}";

        // ---- 绘制 ----
        float width = 300;
        float height = fontSize * 8;
        Rect rect = new Rect(10, 10, width, height);

        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(new Rect(15, 12, width - 10, height - 4), text, textStyle);
    }

    // ============================================================
    // 🎓 动态创建纯色纹理
    //   OnGUI 的 Box 背景需要一个 Texture2D
    //   我们创建一个 2×2 像素的半透明黑色纹理作为背景
    //   2×2 是最小可用尺寸，GPU 会自动拉伸填满整个 Box
    // ============================================================
    private Texture2D MakeTex(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        Texture2D tex = new Texture2D(width, height);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
