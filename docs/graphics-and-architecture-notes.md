# Unity 渲染、Shader 与工程架构知识笔记

> 基于 Unity 6 + URP 的学习记录，涵盖渲染管线、Shader 开发、光照系统、场景架构等主题
> 目标：掌握 3D 游戏开发底层原理，准备游戏开发岗位面试

---

## 目录

1. [寻路系统：NavMesh 与业界选型](#1-寻路系统navmesh-与业界选型)
2. [Shader 基础](#2-shader-基础)
3. [MinimapFlat Shader 解析](#3-minimapflat-shader-解析)
4. [光照系统](#4-光照系统)
5. [渲染路径：Forward / Deferred / Forward+](#5-渲染路径forward--deferred--forward)
6. [烘焙光照与 Light Probe](#6-烘焙光照与-light-probe)
7. [场景架构：单场景 vs 多场景](#7-场景架构单场景-vs-多场景)
8. [场景切换与加载优化](#8-场景切换与加载优化)
9. [小游戏平台优化](#9-小游戏平台优化)
10. [Unity 编辑器扩展](#10-unity-编辑器扩展)
11. [引擎文件格式对比](#11-引擎文件格式对比)
12. [代码打包机制](#12-代码打包机制)
13. [面试常见问题 & 标准回答](#13-面试常见问题--标准回答)

---

## 1. 寻路系统：NavMesh 与业界选型

### Recast Navigation：业界事实标准

**Recast Navigation** 是一个开源 C++ 寻路库，分两个模块：

| 模块 | 作用 |
|------|------|
| **Recast** | 根据场景几何体烘焙 NavMesh |
| **Detour** | 在 NavMesh 上进行 A* 寻路查询 |

🎓 **关键事实**：Unity 和 Unreal Engine 的 NavMesh 底层都基于 Recast Navigation。这是游戏行业寻路的事实标准，GitHub 6000+ Star，License 极宽松（ZLib，商业免费）。

### 业界选型对比

| 规模 | 常见方案 |
|------|---------|
| 小型/独立游戏 | Unity 原生 NavMesh（够用） |
| 中型项目 | **A\* Pathfinding Project**（最流行的第三方库） |
| 大型 AAA | 自研寻路系统，或 DOTS + NavMesh 混合 |

### A* Pathfinding Project 与 NavMesh 的关系

**常见误解**：A* PP 是 Unity NavMesh 的封装。

**实际情况**：两者是**平行独立**的系统，不是上下层关系。

A* PP 有自己的图类型体系：

| 图类型 | 说明 |
|--------|------|
| Grid Graph | 网格图，完全自研 |
| Recast Graph | 自己用 C# 重实现的 Recast 算法，**不调用 Unity NavMesh** |
| NavMesh Graph | 读取外部 Mesh 作导航面，但寻路算法是自己的 |
| Point Graph | 路点图 |

选择 A* PP 的核心原因：运行时动态更新、RVO 人群避让、多种图结构，这些是 Unity 原生 NavMesh 没有的能力。

---

## 2. Shader 基础

### 渲染管线概述

GPU 把 3D 模型变成屏幕像素的固定流程：

```
3D 模型（顶点数据）
    ↓ 【顶点着色器】← 你可以编程：控制形状/位置
    每个顶点：算出屏幕位置
    ↓ 【光栅化】← 硬件自动完成
    把三角形填充成像素
    ↓ 【片段着色器】← 你可以编程：控制颜色
    每个像素：算出颜色
    ↓ 屏幕
```

### 数据类型

```hlsl
float    // 单个小数
float2   // 两个小数（UV 坐标）
float3   // 三个小数（位置 xyz / 颜色 rgb）
float4   // 四个小数（颜色 rgba / 齐次坐标）
half4    // 低精度 float4，颜色常用，省显存
```

Swizzle（分量混排）：
```hlsl
float4 color = float4(1, 0.5, 0, 1);
color.r    // = 1（同 color.x）
color.rgb  // 取前三个分量 = float3
color.bgr  // 反转 RGB
color.rrr  // 复制 R 三次 = float3(1,1,1)
```

### 语义（Semantics）

告诉 GPU"这个数据的用途"：

| 语义 | 含义 |
|------|------|
| `POSITION` | 顶点位置（从 CPU 传入） |
| `SV_POSITION` | 顶点着色器输出的裁剪空间位置 |
| `TEXCOORD0~7` | 通用插值寄存器，可存任意自定义数据 |
| `SV_Target` | 片段着色器输出的颜色（写入屏幕） |

🎓 `TEXCOORD0` 不是"纹理坐标"的意思，而是**通用插值寄存器**——GPU 会在三角形三个顶点之间对该值做重心插值，每个像素自动拿到插值后的结果。

### 顶点 → 片段的数据传递

```hlsl
struct Varyings
{
    float4 positionHCS : SV_POSITION; // 屏幕位置（必须有）
    float  worldY      : TEXCOORD0;   // 自定义数据，GPU 自动插值
    float2 uv          : TEXCOORD1;   // 可以放多个
};
```

### Shader 最小结构

```hlsl
Shader "Custom/MyFirst"
{
    Properties
    {
        _Color ("颜色", Color) = (1, 1, 1, 1)
        _Speed ("速度", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // SRP Batcher 兼容：Properties 必须放在 CBUFFER 里
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
```

### 常用内置函数

```hlsl
saturate(x)             // clamp(x, 0, 1)，GPU 硬件级免费
lerp(a, b, t)           // 线性插值
length(v)               // 向量长度
normalize(v)            // 归一化
dot(a, b)               // 点积（= cos 夹角，光照核心运算）
cross(a, b)             // 叉积（= 垂直于两向量的方向，法线计算）
pow(x, n)               // 幂运算
abs(x)                  // 绝对值

// URP 坐标变换
TransformObjectToHClip(posOS)    // Object → Clip Space（必须做）
TransformObjectToWorld(posOS)    // Object → World Space
```

### 3D 坐标空间流水线

```
Object Space（模型本地坐标）
    ↓ × Model 矩阵（unity_ObjectToWorld）
World Space   ← 光照计算、高度判断在这里
    ↓ × View 矩阵
View Space
    ↓ × Projection 矩阵
Clip Space    ← GPU 在这里决定屏幕位置（SV_POSITION）
    ↓ 透视除法
NDC Space
    ↓ Viewport 变换
Screen Space
```

### Lit vs Unlit

| | URP Lit | URP Unlit |
|--|---------|-----------|
| 光照计算 | ✅ 受光源、阴影、环境光影响 | ❌ 不受任何光照影响 |
| PBR 参数 | Metallic / Smoothness | 无 |
| 性能 | 较高开销 | 极低开销 |
| 适用 | 角色、道具、场景物体 | 特效、UI、粒子、风格化游戏 |
| `_EmissionColor` | ✅ 有 | ❌ 无（本身就"自发光"） |

🎓 **`_EmissionColor` 使用注意**：
```csharp
mat.EnableKeyword("_EMISSION");                // 必须先开启，否则无效
mat.SetColor("_EmissionColor", Color.white * 3f); // HDR 颜色，超过1才能触发 Bloom
```

### RenderQueue（渲染队列）

| 名称 | 数值 | 用途 |
|------|------|------|
| Background | 1000 | 天空盒 |
| Geometry | 2000 | 普通不透明物体（默认） |
| AlphaTest | 2450 | 镂空物体（草、铁丝网） |
| Transparent | 3000 | 半透明物体 |
| Overlay | 4000 | 最后渲染（光晕、UI） |

🎓 透明物体必须排在不透明物体之后渲染，否则颜色混合结果错误——这是 RenderQueue 存在的核心原因。

---

## 3. MinimapFlat Shader 解析

### 核心思路

```
世界空间 Y 坐标 → saturate 归一化到 0~1 → lerp 两个颜色
低处（地面）→ 深蓝灰，高处（墙顶）→ 亮白
俯视看下去，地形高低一目了然
```

### 数据流

```
网格顶点（Object Space）
    ↓ vert()
    ├─ TransformObjectToHClip → positionHCS（告诉 GPU 像素画哪里）
    └─ TransformObjectToWorld → worldY（提取真实世界高度）
    ↓ GPU 在三角形内自动插值 worldY
每个像素拿到插值后的 worldY
    ↓ frag()
    ├─ t = saturate((worldY - HeightMin) / (HeightMax - HeightMin))
    └─ return lerp(_ColorLow, _ColorHigh, t)
```

### 关键代码

```hlsl
Varyings vert(Attributes input)
{
    Varyings output;
    // 两次变换，两个用途
    output.positionHCS = TransformObjectToHClip(input.positionOS.xyz); // 定位
    float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
    output.worldY = worldPos.y;  // 存高度，等待插值
    return output;
}

half4 frag(Varyings input) : SV_Target
{
    float t = saturate((input.worldY - _HeightMin) / (_HeightMax - _HeightMin));
    return lerp(_ColorLow, _ColorHigh, t);
}
```

🎓 **为什么需要两次坐标变换**：`positionHCS` 是 GPU 定位像素用的，`worldY` 是我们自己算颜色用的，两个用途、两个目标空间，不能合并。

---

## 4. 光照系统

### 光照方程组成

```
最终颜色 = 自发光（Emission）
         + 环境光（Ambient）
         + 漫反射（Diffuse）
         + 高光反射（Specular）
```

### 漫反射：Lambert 模型

光照核心公式——**点积**：

```hlsl
float NdotL = max(0, dot(normalize(normal), normalize(lightDir)));
float3 diffuse = lightColor * albedo * NdotL;
```

🎓 `dot(normal, lightDir)` = cos(夹角)。正对着照夹角=0°，cos=1，最亮；斜射夹角大，cos小，较暗。这是光照计算中最核心、最频繁的运算。

### 高光反射：Blinn-Phong 模型

```hlsl
float3 halfDir = normalize(lightDir + viewDir); // 半程向量
float NdotH   = max(0, dot(normal, halfDir));
float specular = pow(NdotH, _Shininess);       // Shininess越大，高光越集中
```

### PBR（物理基础渲染）

URP Lit 使用的光照模型，参数有物理意义：

| 参数 | 值 | 含义 |
|------|-----|------|
| **Metallic** | 0 | 非金属（木头、石头），高光白色，有漫反射 |
| **Metallic** | 1 | 金属（铁、金），高光染albedo色，无漫反射 |
| **Smoothness** | 0 | 粗糙磨砂，高光散、面积大 |
| **Smoothness** | 1 | 镜面光滑，高光集中、面积小 |

🎓 PBR 的核心优势是**能量守恒**：反射能量不超过入射能量。美术调一次参数，在任何光照环境下材质表现都一致。

### 法线的来源

| 类型 | 来源 | 特点 |
|------|------|------|
| 面法线 | 三角形两边叉积实时计算 | 边界处光照突变，有明显棱角 |
| 顶点法线 | 建模时预计算，存入模型文件 | 相邻面法线取平均，光照平滑过渡 |
| 法线贴图 | 每像素从贴图读取 | 低模模拟高模细节，精度最高 |

🎓 建模软件的"硬边/软边"本质就是控制顶点法线：软边=相邻面法线取平均，硬边=顶点分裂各保留各自法线。法线贴图蓝紫色是因为大部分表面法线接近 (0,0,1)，对应颜色 (128,128,255)。

### 阴影：Shadow Map

```
第一步（Shadow Pass）：从光源视角渲染场景，存储每像素深度
第二步（正常渲染）：把当前像素变换到光源空间，比较深度
    当前深度 > Shadow Map 深度 → 被遮挡 → 阴影
    当前深度 ≤ Shadow Map 深度 → 可见 → 受光
```

常见问题：**Shadow Acne（阴影痤疮）**——Shadow Map 精度有限导致自遮挡浮点误差，表面出现条纹。解决方法：加 Bias 偏移量。

---

## 5. 渲染路径：Forward / Deferred / Forward+

### 核心区别

```
Forward：  几何 和 光照  在同一个 Pass 里一起处理
Deferred： 几何 和 光照  在不同阶段分开处理
Forward+:  Forward 基础上增加 Tile-based 灯光剔除
```

### Forward（前向渲染）

```
每个物体 → [ForwardLit Pass：对所有灯循环计算] → 屏幕

100个物体 × 20个光源 = 2000次光照计算
灯多了只是循环次数增加，不增加 Pass 数
```

光照数据通过 Uniform 每帧传入：
```hlsl
_MainLightPosition        // 主光源方向
_MainLightColor           // 主光源颜色
_AdditionalLightsCount    // 额外光源数量
_AdditionalLightsPosition // 额外光源位置数组
```

### Deferred（延迟渲染）

```
第一阶段：Geometry Pass（每个物体执行一次）
  → 不计算光照，只把材质数据写入 G-Buffer：
      RT0: Albedo | RT1: 法线 | RT2: Metallic/Smoothness | RT3: 深度

第二阶段：Lighting Pass（每个光源执行一次）
  → 读取 G-Buffer，计算该光源贡献，累加

场景有 12 个点光源：
  Deferred = 1个GeometryPass + 12个LightingPass（灯光数量解耦）
  Forward  = 1个ForwardLit Pass，Shader内循环12次
```

**Deferred 的代价**：
- 不支持透明物体（G-Buffer 每像素只能存一层，需要 Forward 补充渲染）
- 不支持 MSAA（G-Buffer 阶段无法做硬件抗锯齿）
- 显存占用高（多张 RT）
- 移动端不友好（带宽敏感）

### Forward+

在 Forward 基础上增加 **Tile-Based Light Culling**：

```
第一阶段：Light Culling（Compute Shader，每帧一次）
  把屏幕分成 16×16 像素的 Tile
  计算每个 Tile 内有哪些光源 → 存入 Light Grid

第二阶段：ForwardLit Pass
  每个像素查询 Light Grid，只循环所在 Tile 的灯
  跳过无关光源

效果：100物体 × 20灯 → 实际只处理平均5灯 = 500次（节省75%）
```

### 对比总结

| | Forward | Forward+ | Deferred |
|--|---------|---------|---------|
| 多光源性能 | ❌ 差 | ✅ 好 | ✅ 好 |
| 透明物体 | ✅ | ✅ | ⚠️ 需补丁 |
| MSAA | ✅ | ✅ | ❌ |
| 移动端 | ✅ 友好 | ✅ | ❌ |
| 依赖 Compute Shader | 无 | 需要 | 无 |

🎓 **行业趋势**：2020年代 Forward+/Clustered 逐渐取代 Deferred，主要驱动力是透明物体处理灵活性。Unity 6 URP 正式支持 Forward+，新项目推荐使用。

---

## 6. 烘焙光照与 Light Probe

### 实时 vs 烘焙

```
实时光照：每帧重新计算，支持动态变化，性能开销大
烘焙光照：提前计算存成 Lightmap 贴图，运行时采样，几乎零开销
          代价：不支持动态变化
```

### 三种光照模式

| 模式 | 说明 | 适用 |
|------|------|------|
| Realtime | 完全实时 | 会移动/变色的灯 |
| Baked | 完全烘焙 | 纯静态场景 |
| **Mixed (Shadowmask)** | 静态烘焙阴影 + 实时直接光 | 本项目使用 |

### Shadowmask 模式（本项目方案）

```
烘焙阶段：把静态物体的阴影存入 Shadowmask 贴图
运行时：
  静态物体 → 直接采样 Shadowmask（零开销）
  动态物体 → 实时阴影（保留精确）

本项目效果：
  12个点光源阴影烘焙 → 节省18个 Draw Call（-41%）
```

### 设置步骤

```
1. 选中静态物体 → Inspector 右上角勾选 Static → Contribute GI
2. 选中光源 → Light 组件 → Mode → Mixed
3. Window → Rendering → Lighting
   ├── Lighting Mode → Shadowmask
   ├── Lightmap Resolution → 40
   └── Lightmap Size → 1024
4. 底部 Generate Lighting 开始烘焙
```

### Light Probe

让动态物体（角色、敌人）接收烘焙环境光，视觉上融入场景：

```
GameObject → Light → Light Probe Group
在场景里放置若干绿色球（探针点）
烘焙后：动态物体移动时插值周围探针，获得对应位置的环境光颜色
```

**放置技巧**：

| 技巧 | 说明 |
|------|------|
| 跟着光照变化走 | 阴影边界、门口、颜色交界处密集；均匀区域稀疏 |
| 沿角色路径放 | 只在可行走区域放，墙里不放 |
| 垂直两层 | 高度 y=0.3 和 y=2.0，支持跳跃时的垂直插值 |
| 门口加密 | 过渡区域 Probe 稀疏会导致光照"闪" |
| 不放几何体内部 | 墙内 Probe 会采样到全黑，影响附近物体 |

🎓 **球谐函数（Spherical Harmonics）**：每个 Probe 存储的不是颜色，而是 SH 系数（27个float）——把该点全向光照压缩成低频近似。查询时传入法线方向，输出该方向的环境光颜色。SH 是模糊的，适合环境光，不适合精确阴影。

---

## 7. 场景架构：单场景 vs 多场景

### .unity 文件

一个 `.unity` 文件 = 一个 Unity Scene，存储：
- 所有 GameObject 及其层级关系
- 每个 GameObject 上的 Component 和参数
- 光照设置引用
- NavMesh 数据

### 单场景

**优点**：开发简单，引用直接拖拽，调试方便
**缺点**：所有资源同时占用内存；多人协作冲突严重；无法按需流式加载；小游戏首包装不下

### 多场景（Additive 加载）

```csharp
// Additive：保留当前场景，叠加加载新场景
SceneManager.LoadSceneAsync("Level_02", LoadSceneMode.Additive);
```

**推荐架构**：

```
GameCore.unity（永远不卸载）
  ├── GameManager、AudioManager、EventSystem
  └── 共用 UI、常驻数据

按需加载/卸载：
  ├── MainMenu.unity
  ├── Level_01.unity
  └── Level_02.unity
```

**切换关卡**：

```csharp
async Task SwitchLevel(string from, string to)
{
    await SceneManager.LoadSceneAsync(to, LoadSceneMode.Additive);   // 先加载新的
    await SceneManager.UnloadSceneAsync(from);                        // 再卸载旧的
}
```

### 关卡复用方案

| 方案 | 适用场景 |
|------|---------|
| **Prefab** | 所有可复用的物体做成 Prefab，修改一处全关卡同步 |
| **Prefab Variant** | 基础 Prefab + 局部差异覆盖（精英敌人继承普通敌人） |
| **Scene Template** | 每个关卡从模板创建，预置光照/NavMesh/管理器 |
| **程序化生成** | Roguelike 最佳方案：房间做成 Prefab 模块，算法随机拼接 |

### 跨场景引用方案

多场景无法直接拖拽引用，常见解法：

| 方案 | 原理 | 适用 |
|------|------|------|
| 单例（Singleton） | GameManager.Instance 全局访问 | 管理类 |
| ScriptableObject 事件 | 共享资产，不依赖场景 | 解耦通信 |
| DontDestroyOnLoad | 切场景不销毁 | 简单项目 |
| ServiceLocator | 注册/查询服务 | 中大型项目 |

---

## 8. 场景切换与加载优化

### 默认加载的代价

```
LoadScene("Level_02") 执行时（主线程同步，期间完全卡住）：
1. 销毁当前场景所有 GameObject
2. 卸载不再需要的资源，触发 GC
3. 从磁盘读取新场景文件（IO）
4. 反序列化 + 实例化所有 GameObject
5. 加载依赖的贴图/模型/音频
6. 执行所有 Awake / Start
7. 渲染第一帧
```

### 优化层次

**第一层：异步加载**

```csharp
AsyncOperation op = SceneManager.LoadSceneAsync("Level_02");
op.allowSceneActivation = false; // 加载完不自动切换

// 在 Loading 动画播放完后再真正切换
op.allowSceneActivation = true;
```

**第二层：常驻场景架构**

```
单场景切换：销毁+重建 100% 的对象
常驻场景架构：只销毁+重建关卡独有部分（20~30%）
→ 加载时间直接减少 70%
```

**第三层：Addressables 资源缓存**

```csharp
// 只要不 Release，资源就不会被卸载
var handle = Addressables.LoadAssetAsync<Texture2D>("dungeon-wall");
await handle.Task;
// 切场景也不影响，两个关卡共用同一份贴图不重复加载
```

**第四层：对象池**

```csharp
// Unity 6 内置 ObjectPool
var pool = new ObjectPool<Enemy>(
    createFunc: () => Instantiate(prefab),
    actionOnGet: e => e.gameObject.SetActive(true),
    actionOnRelease: e => e.gameObject.SetActive(false)
);

Enemy e = pool.Get();    // 激活，不创建
pool.Release(e);          // 隐藏，不销毁
```

**第五层：预加载（接近秒切）**

```csharp
// 进入第一关时，后台预加载第二关
AsyncOperation preloadedOp = SceneManager.LoadSceneAsync("Level_02", LoadSceneMode.Additive);
preloadedOp.allowSceneActivation = false; // 加载但不激活，不影响游戏

// 过关时直接激活，几乎零等待
preloadedOp.allowSceneActivation = true;
```

---

## 9. 小游戏平台优化

### 首包大小限制（以微信小游戏为例）

```
首包：≤ 20MB（代码 + 必要资源）
分包：≤ 30MB / 个
总包：≤ 150MB
其余：从 CDN 远程加载
```

### 启动速度限制

```
白屏时间：< 5 秒（超过微信会警告/下架）
解法：首包内置最小化 Loading 界面（背景色 + Logo + 进度条）
     用户看到 Loading 界面，白屏立刻结束
     后台并行下载首屏资源，进度条显示真实进度
```

### 分层加载架构

```
0s：首包代码 + Boot 场景（本地，瞬间）→ 用户看到 Loading ✅
0~Ns：后台下载首屏必需资源
  有缓存 → 极快（<1s）
  无缓存 → 取决于网速，进度条显示
Ns：进入游戏
游戏过程中：静默预加载后续关卡（边玩边下）
```

### 代码体积优化

```
Project Settings → Player → Other Settings：

Scripting Backend → IL2CPP（比 Mono 体积小，性能好）

Managed Stripping Level → Medium
  → 剔除未使用的 C# 代码

Strip Engine Code → ✅
  → 剔除未使用的引擎模块（不用 NavMesh 就剔掉 NavMesh）
  → 能省 2~10 MB
```

**link.xml（保护不能被剔除的代码）**：

```xml
<linker>
  <assembly fullname="Assembly-CSharp">
    <type fullname="MyGame.NetworkManager" preserve="all"/>
  </assembly>
</linker>
```

### 资源体积优化

| 类型 | 优化方案 | 效果 |
|------|---------|------|
| 贴图 | ASTC 6x6 压缩 | 体积减少 80%（RGBA32→ASTC） |
| 背景音乐 | Streaming + Vorbis | 不预加载，流式播放 |
| 音效 | Decompress on Load + ADPCM | 短文件，提前解压快速播放 |
| 模型 | Mesh Compression High | 顶点数据压缩 |
| Shader | Shader Variant Collection | 只打包实际用到的变体（隐藏大头） |

🎓 **Shader 变体是常被忽视的隐藏大头**：URP Lit 有数百个变体组合，全部打包可达 10MB+。用 Shader Variant Collection 收录实际用到的变体，开启 Strip Unused Shader Variants。

### WebGL 特有优化

```
Compression Format：
  Brotli → 比 Gzip 小 20%（需服务器支持）

Exception Support：
  Explicitly Thrown Only → 平衡性能与调试

Development Build：❌ 关闭（去掉调试符号）
```

---

## 10. Unity 编辑器扩展

### 本质

```
普通 C# 脚本：打包进游戏，运行时执行
Editor Script：只存在于编辑器，打包时完全剔除，不进入最终游戏
```

Assets 的 `Editor` 文件夹下的脚本，或带 `#if UNITY_EDITOR` 的代码，都属于 Editor 域。

### 三种运行时机

**时机一：菜单触发**
```csharp
[MenuItem("Tools/批量设置敌人血量")]
static void SetAllEnemyHP()
{
    var enemies = GameObject.FindObjectsOfType<EnemyController>();
    foreach (var e in enemies)
    {
        Undo.RecordObject(e, "Set Enemy HP"); // 支持撤销
        e.maxHp = 100;
        EditorUtility.SetDirty(e);
    }
}
```

**时机二：编辑器事件自动触发**
```csharp
[InitializeOnLoadMethod]
static void OnEditorLoaded()
{
    // 编辑器启动/脚本重编译后自动执行
    // 常用于注册全局编辑器事件
}

[PostProcessBuild]
static void OnBuildFinished(BuildTarget target, string path)
{
    // 构建完成后自动执行
}
```

**时机三：自定义 Inspector**
```csharp
[CustomEditor(typeof(EnemyController))]
public class EnemyControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("测试：立刻死亡"))
            ((EnemyController)target).TakeDamage(99999);
    }

    void OnSceneGUI()
    {
        // 在 Scene 视图画攻击范围圆圈（仅编辑器可见）
        var e = (EnemyController)target;
        Handles.DrawWireDisc(e.transform.position, Vector3.up, e.attackRange);
    }
}
```

### SerializedObject（支持 Undo 的正确写法）

```csharp
// 直接 target.field = value → 不支持 Ctrl+Z
// 通过 SerializedObject → 自动支持 Undo/Redo 和 Prefab 脏标记

serializedObject.Update();
EditorGUILayout.PropertyField(serializedObject.FindProperty("moveSpeed"));
serializedObject.ApplyModifiedProperties();
```

### 编译域分离

```
Runtime 域 → Assets/**/*.cs（非 Editor 文件夹）→ 打包进游戏
Editor 域  → Assets/**/Editor/**/*.cs            → 仅编辑器

Runtime 代码不能引用 UnityEditor（打包后 UnityEditor.dll 不存在）
需要引用时用 #if UNITY_EDITOR 包裹
```

🎓 你改了一行代码 Unity 会短暂"转圈"，就是在重编译并重新执行所有 `[InitializeOnLoad]` 的静态构造函数，重建编辑器状态。

---

## 11. 引擎文件格式对比

### 核心差异：引用系统

| 引擎 | 引用方式 | 直接修改安全性 |
|------|---------|--------------|
| **Godot** | 相对路径字符串（`res://player.tscn`） | ✅ 安全，改路径字符串即可 |
| **Unity** | GUID（`.meta` 文件，不透明哈希） | ⚠️ 危险，需要知道正确 GUID |
| **Cocos** | UUID | ⚠️ 有风险，跨文件引用易断裂 |

### 为什么 Godot 对 AI 更友好

```
Godot .tscn 文件：
  [node name="Player" type="CharacterBody3D"]
  transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 2, 0)
  → 极简语义化，一眼看懂，路径引用清晰

Unity .unity 文件：
  m_LocalPosition: {x: 0, y: 2, z: 0}
  m_Material: {fileID: 2100000, guid: a3f4b2c1..., type: 2}
  → 字段带 m_ 前缀，引用是不透明 GUID，改错就断裂
```

🎓 **这是主动的设计取舍，不是技术限制**：
- Unity 选择 GUID：工程稳定性优先，重命名文件不断引用
- Godot 选择路径：开放性优先，文件对工具链友好

Godot 4 引入了 `.uid` 文件作为补充（类似 Unity GUID），证明路径引用在工程实践中确实有痛点，两种方案各有取舍。

### 直接修改 Unity 文件的安全边界

```
✅ 安全：改纯数值（位置/颜色/参数），不涉及 GUID 引用，Unity 已关闭
❌ 危险：添加/删除 Component，添加资源引用，改变层级结构

更好的方案：写 Editor Script，用 Unity API 操作，自动处理 GUID、Undo、脏标记
```

---

## 12. 代码打包机制

### C# 代码不进 AssetBundle

```
AssetBundle 装的是资源（贴图、模型、Prefab、音频）
C# 代码统一编译成 .NET Assembly（.dll 文件）打进主包

AssetBundle 里可以有挂了脚本的 Prefab，
但 Prefab 里存的是组件引用（GUID），不是脚本代码本身
```

### 构建流程

```
所有 .cs 文件
    ↓ Roslyn 编译器
Assembly-CSharp.dll              ← 你写的游戏代码
Assembly-CSharp-firstpass.dll    ← Plugins 文件夹里的代码
各插件的独立 .dll                 ← 第三方插件（如果有 asmdef）
    ↓ IL2CPP（如果开启）
C++ 代码 → 平台原生二进制
```

### Assembly Definition（asmdef）

把代码拆分成多个独立编译单元，加速增量编译：

```
默认：所有代码 → Assembly-CSharp.dll（任何改动全量重编译，慢）

用 asmdef 分包：
  MyGame.Runtime.asmdef → MyGame.Runtime.dll
  MyGame.Editor.asmdef  → MyGame.Editor.dll（仅编辑器）
  MyGame.Tests.asmdef   → MyGame.Tests.dll（仅测试）

改了 MyGame.Runtime → 只重编译这一个 dll → 大项目从30秒→3秒
```

### 热更新方案

```
普通 AssetBundle / Addressables：只能热更资源，代码已打进主包

热更新代码的方案：
  HybridCLR（原 Huatuo）← 国内最主流
    → 把 C# dll 当资源下载，运行时解释执行

  ILRuntime
    → 独立的 C# 解释器，加载外部 dll

  Lua（xLua / toLua）
    → 绕开 C#，用 Lua 写热更逻辑
```

🎓 热更新在国内手游行业是刚需（审核周期长，Bug 必须能快速修复）。海外项目相对少见，因为平台审核更宽松。

---

## 13. 面试常见问题 & 标准回答

### Q1：Unity NavMesh 和 A* Pathfinding Project 是什么关系？

> Unity NavMesh 底层基于 Recast Navigation，A* Pathfinding Project 是完全独立的系统，不依赖 Unity NavMesh/Recast 底层。A* PP 自己用 C# 实现了 Recast 风格的 NavMesh 烘焙（Recast Graph）和 Grid Graph 等多种图结构。选择 A* PP 的核心原因是运行时动态更新和 Agent 流量控制，这是原生 NavMesh 没有的能力。

### Q2：Shader 中 TEXCOORD0 是什么意思？

> TEXCOORD0 不是"纹理坐标"的意思，而是通用插值寄存器——告诉 GPU 请在三角形顶点之间对这个值做重心插值，每个像素自动拿到插值后的结果。TEXCOORD0~7 都是通用寄存器，可以存任何自定义数据，不一定是 UV 坐标。

### Q3：Forward、Deferred、Forward+ 的区别和选择？

> Forward 把几何和光照在同一 Pass 里处理，灯多了 Shader 循环次数增加，不增加 Pass 数。Deferred 解耦两者：Geometry Pass 写 G-Buffer，然后每个光源独立一个 Lighting Pass，适合大量动态光源，代价是不支持透明物体和 MSAA。Forward+ 在 Forward 基础上用 Compute Shader 做 Tile-based 光源剔除，每像素只处理所在 Tile 的光源，多光源性能接近 Deferred 同时保留透明物体支持。Unity 6 的 URP 推荐 Forward+。

### Q4：Light Probe 的作用和放置原则？

> Light Probe 让动态物体接收烘焙环境光，视觉上融入静态烘焙场景。每个 Probe 存储球谐函数（27个float的低频光照近似），运行时动态物体插值周围探针。放置原则：沿角色行走路径放，垂直两层（地面和腰部高度），光照变化快的地方（阴影边界、门口）密集，均匀区域稀疏，不要放进几何体内部。

### Q5：场景切换如何做到不黑屏、快速加载？

> 分几个层次：第一，用 LoadSceneAsync 异步加载，配合 allowSceneActivation=false 在后台加载完成后才切换，让用户看到进度条而非黑屏。第二，常驻场景架构把管理器放在永不卸载的 GameCore 场景里，切关卡只换关卡独有内容，减少 70% 的重建量。第三，Addressables 手动管理资源生命周期，跨关卡复用的资源常驻内存不重复加载。第四，在玩家即将过关前预加载下一关并用 allowSceneActivation=false 挂起，过关时直接激活实现秒切。

### Q6：小游戏首包如何极致压缩？

> 分几个层次：代码层用 IL2CPP + Managed Stripping 剔除未使用代码和引擎模块；资源层用 Addressables 把非启动必需资源转为远程加载，首包只保留 Loading 场景和核心 UI；资源本身用 ASTC 压缩贴图、Streaming 模式处理音频。Shader 变体是常被忽视的隐藏大头，要用 Shader Variant Collection 精确收录。微信小游戏还支持原生分包机制，可配合 Unity 官方插件拆分代码和资源包。

### Q7：Unity Editor Script 的原理是什么？

> Editor Script 运行在编辑器的独立编译域，打包时完全剔除。运行时机分三类：MenuItem 特性注册菜单项，点击时触发；InitializeOnLoad 在编辑器启动和脚本重编译后自动触发；CustomEditor 重写 OnInspectorGUI，在 Inspector 刷新时每帧调用。修改数据应通过 SerializedObject 而非直接赋值，这样能自动支持 Undo/Redo 和 Prefab 脏标记。Unity 的编辑器扩展 API 对所有人完全开放，Asset Store 插件和自己写的 Editor Script 用完全相同的机制。

### Q8：Godot 为什么比 Unity 更容易被 AI 直接修改文件？

> 核心原因是引用系统设计不同。Godot 用路径字符串引用资源，AI 看到 `res://player.tscn` 就知道引用的是什么，修改只需改字符串。Unity 用 GUID（.meta 文件里的哈希值）引用资源，AI 无法在不运行引擎的情况下知道哪个文件对应哪个 GUID，改错 GUID 会导致引用断裂。这是两种工程哲学的取舍：Godot 优先可读性和工具友好性，Unity 优先大规模项目的引用稳定性（重命名文件不断引用）。Godot 4 引入 .uid 文件也在向 GUID 方向靠拢。
