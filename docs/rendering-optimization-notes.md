# 渲染优化实践记录

> 项目：Dungeon Slash（Unity 6 URP 俯视角 3D Roguelike）
> 更新日期：2026-04-02

---

## 1. 小地图相机优化

**文件**：`Assets/Scripts/UI/MinimapCamera.cs`

### 1.1 独立 Renderer

- 小地图使用 `MinimapRenderer`（Forward 渲染），与主相机 `PC_Renderer`（Deferred）分离
- 通过 `UniversalAdditionalCameraData.SetRenderer(index)` 指定
- MinimapRenderer 只渲染 Layer 6 & 7（CullingMask = 192），无 Renderer Features

### 1.2 关闭阴影和后处理（管线层优化）

```csharp
urpCameraData.renderShadows = false;
urpCameraData.renderPostProcessing = false;
```

- **原理**：即使 Shader 是 Unlit，URP 管线仍会为每个相机准备 Shadow Map 和光源数据。`renderShadows = false` 在管线层面跳过整个 Shadow Pass，而不只是 Shader 层面不用光照数据
- **效果**：小地图相机渲染从 ~60 batches 降至 ~5 batches

### 1.3 降频渲染

```csharp
cam.enabled = (frameCount % renderInterval == 0); // renderInterval = 3
```

- 每 3 帧渲染一次，RenderTexture 保留上一次内容
- 60fps 下小地图以 20fps 刷新，肉眼无感知
- 平均每帧 batches 贡献降至 ~1.7

### 1.4 移除多余 AudioListener

```csharp
var listener = GetComponent<AudioListener>();
if (listener != null) Destroy(listener);
```

- 场景只能有 1 个 AudioListener，小地图相机不需要

### 1.5 自定义 Unlit Shader

**文件**：`Assets/Shaders/MinimapFlat.shader`、`Assets/Shaders/MinimapMarker.shader`

- MinimapFlat：按世界空间 Y 坐标做高度→颜色映射，`Lighting Off`
- MinimapMarker：纯色标记，`ZTest Always` 保证不被地形遮挡
- 两个 Shader 都使用 `CBUFFER_START(UnityPerMaterial)` 兼容 SRP Batcher

---

## 2. 光源优化

### 2.1 关闭附加光源阴影

- 场景有 15 个 Point Light，其中 12 个开了阴影
- 12 个灯的阴影共享 2048×2048 Shadow Atlas → 分辨率被迫降低
- **操作**：全部 Point Light 的 Shadow Type 改为 No Shadows
- **效果**：Additional Lights Shadowmap 从 18 batches → 0（整行消失）
- **总 Batches**：44 → 26，降了 41%

### 2.2 Frame Debugger 分析

一帧渲染流程及 Batches 分布（优化前 44 batches）：

| 阶段 | Batches | 占比 | 性质 |
|------|---------|------|------|
| Main Light Shadowmap | 4 | 9% | 主光阴影（4 级 Cascade） |
| Additional Lights Shadowmap | 18 | 41% | 🔴 **附加光阴影（已优化掉）** |
| DepthNormalPrepass | 2 | 5% | SSAO 前置 |
| SSAO | 4 | 9% | 环境光遮蔽 |
| DrawOpaqueObjects | 2 | 5% | 实际画物体 |
| DrawSkybox | 1 | 2% | 天空盒 |
| CopyColor | 1 | 2% | 全屏复制 |
| DrawTransparentObjects | 6 | 14% | 透明物体 |
| BlitFinalToBackBuffer | 1 | 2% | 输出到屏幕 |
| DrawScreenSpaceUI | 5 | 11% | UI |

### 2.3 已了解但未实施的方案

| 方案 | 效果 | 备注 |
|------|------|------|
| 减少 Shadow Cascade（4→2） | 省 ~2 batches | 远处阴影精度降低 |
| 减小 Shadow Distance（50→25） | 减少参与阴影的物体数 | 俯视角视野有限，50 太大 |
| 降低阴影分辨率（2048→1024） | 不减 Batches，降 GPU 填充率 | 阴影边缘模糊 |
| 关闭 SSAO | 省 6 batches | 角落立体感消失 |
| 烘焙光照（Baked Lighting） | 省 18+ batches | 复杂度高，只对静态物体有效 |
| Light Layers | 减少每灯渲染物体数 | 场景复杂时再用 |

---

## 3. 合批（Batching）策略

| 物体类型 | 合批方式 | 原因 |
|---------|---------|------|
| 地形、墙壁、柱子 | Static Batching | 不会动，顶点预合并 |
| 玩家、敌人 | SRP Batcher | 会动，Shader 兼容 CBUFFER |
| 大量同模型敌人 | GPU Instancing | 同 Mesh + 同 Material |
| Dynamic Batching | 不使用 | CPU 开销大，URP 默认关闭 |

**SRP Batcher 原理**：不减少 Draw Call 数量，而是缓存材质属性在 GPU Constant Buffer 中，减少每个 Draw Call 之间的 CPU SetPass 开销。

---

## 4. 多平台渲染配置

**文件**：`Assets/Settings/` 目录

### 4.1 架构

```
Quality Settings（Edit → Project Settings → Quality）
  ├─ "PC" Level       → PC_RPAsset       → PC_Renderer + MinimapRenderer
  │   └─ 平台：Standalone
  ├─ "Mobile" Level   → Mobile_RPAsset   → Mobile_Renderer + MinimapRenderer
  │   └─ 平台：Android, iOS
  └─ "WebGL" Level    → WebGL_RPAsset    → WebGL_Renderer + MinimapRenderer
      └─ 平台：WebGL
```

MinimapRenderer 跨平台共用（已是最精简的 Forward Unlit，无平台差异可调）。

### 4.2 参数对比

| 设置 | PC | Mobile | WebGL |
|------|:---:|:------:|:-----:|
| 渲染模式 | Deferred | Forward | Forward |
| 主光阴影分辨率 | 2048 | 1024 | 512 |
| Shadow Cascade | 4 | 1 | 1 |
| Shadow Distance | 50 | 50 | 20 |
| 附加光阴影 | ✅ | ❌ | ❌ |
| 软阴影 | ✅ 5×5 PCF | ❌ | ❌ |
| SSAO | ✅ | ❌ | ❌ |
| HDR | ✅ | ✅ | ❌ |
| MSAA | 关 | 关 | 2x |
| Render Scale | 1.0 | 0.8 | 0.7 |
| Per Object Light Limit | 4 | 4 | 2 |
| Depth Texture | ✅ | ❌ | ❌ |
| Opaque Texture | ✅ | ❌ | ❌ |
| Post Processing（Renderer 层） | ✅ | ✅ | ❌ |

### 4.3 为什么 Mobile/WebGL 用 Forward 而非 Deferred

- Deferred 需要 G-Buffer（4 张全屏纹理 ≈ 33MB 显存）
- 移动 GPU 是 Tile-Based 架构，G-Buffer 的多次读写抵消了 Tile Memory 优势
- 灯少（附加光阴影都关了）时 Forward 更高效

---

## 5. 相机遮挡透明化

**文件**：`Assets/Scripts/Camera/CameraOcclusionHandler.cs`

- **原理**：每帧从相机向玩家做 SphereCast，碰到的物体材质切换为半透明
- **URP 透明切换要点**：必须同时修改 `_Surface`、`_SrcBlend`、`_DstBlend`、`_ZWrite`、`RenderQueue` 和 `_SURFACE_TYPE_TRANSPARENT` Keyword，缺一不可
- **性能优化**：`SphereCastNonAlloc` + 预分配数组避免 GC
- **Lerp 平滑过渡**：不是瞬间切透明，逐帧逼近目标值
- **OnDisable 恢复材质**：Material 是共享资源，必须恢复防止污染

---

## 6. 调试工具

**文件**：`Assets/Scripts/UI/DebugStatsUI.cs`

- OnGUI 即时模式显示 FPS、帧时间、分辨率、Quality Level、GPU 信息
- `Debug.isDebugBuild` 判断 Development Build，Release 下自动隐藏
- `Time.unscaledDeltaTime` 不受 timeScale 影响
- F1 键切换显示/隐藏
- FPS 颜色动态变化：绿（≥55）、黄（≥30）、红（<30）

---

## 7. WebGL 构建注意事项

### 7.1 压缩格式

| 格式 | 文件大小 | 兼容性 |
|------|---------|--------|
| Brotli (.br) | 最小 | 需 HTTPS + 服务器支持 |
| Gzip (.gz) | 中等 | ✅ GitHub Pages 兼容 |
| Disabled | 最大 | 所有环境 |

- 本地测试和 GitHub Pages 部署用 **Gzip**
- Build and Run 使用内置 `SimpleWebServer.exe`（自动处理 .br/.gz 响应头）

### 7.2 编译链路

```
C# → IL2CPP → C++ → Emscripten → WebAssembly (.wasm)
```

两次编译导致 WebGL 构建慢，但支持增量构建（缓存在 Library/ 目录）。
