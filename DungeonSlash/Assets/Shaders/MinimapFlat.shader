// ============================================================
// MinimapFlat.shader — 小地图地形着色器（按高度映射颜色）
// ============================================================
//
// 🎓 核心原理：高度映射（Height-based Coloring）
//   不再输出固定颜色，而是根据每个像素在世界空间中的 Y 坐标
//   映射到 "深色 → 亮色" 的渐变。低处暗、高处亮，
//   从上往下看自然就能看出地形的高低和边界。
//
//   这和地理学中的"等高线地图"是同一个思路：
//   用颜色深浅表示海拔高低。
//
// 🎓 关键技术点：顶点着色器传递世界坐标给片段着色器
//   模型顶点的原始坐标是 Object Space（模型本地坐标）
//   我们需要 World Space 的 Y 坐标来做高度判断
//   所以在顶点着色器里做 Object → World 变换，
//   把世界坐标通过 Varyings 传给片段着色器
//
// 🎓 对比 2D 开发：
//   在 Cocos/Godot 2D 中，所有坐标都是屏幕/世界坐标，不需要变换
//   3D 中有多个坐标空间：Object → World → View → Clip → Screen
//   Shader 编程的核心就是在这些空间之间转换
// ============================================================

Shader "Custom/MinimapFlat"
{
    Properties
    {
        // 🎓 这些参数可以在 Material Inspector 中调节
        // 不用改代码就能微调小地图的视觉效果

        _ColorLow  ("低处颜色", Color) = (0.15, 0.15, 0.2, 1)
        // 地面最低处的颜色（深蓝灰，像深水区）

        _ColorHigh ("高处颜色", Color) = (0.9, 0.9, 0.85, 1)
        // 最高处的颜色（亮白，像雪山顶）

        _HeightMin ("最低高度", Float) = 0
        // 场景中最低点的 Y 值（低于此值的都显示为 _ColorLow）

        _HeightMax ("最高高度", Float) = 10
        // 场景中最高点的 Y 值（高于此值的都显示为 _ColorHigh）
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "MinimapHeight"
            Lighting Off
            Fog { Mode Off }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---- 数据结构 ----

            struct Attributes
            {
                float4 positionOS : POSITION;  // 模型本地坐标
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION; // 裁剪空间坐标（GPU 用来确定屏幕位置）
                float  worldY      : TEXCOORD0;   // 世界空间 Y 坐标（我们自定义的数据）
                // 🎓 TEXCOORD0 是"语义"（Semantic）
                //   不是说这个数据一定是纹理坐标，而是告诉 GPU：
                //   "请在三角形内部对这个值做插值"
                //   TEXCOORD0~7 是通用插值寄存器，可以放任何自定义数据
                //   面试说法："TEXCOORD 语义用于在顶点间插值任意数据"
            };

            // 🎓 CBUFFER (SRP Batcher 兼容)
            // 所有 Properties 中声明的变量必须放在这里
            // 变量名必须和 Properties 中的名字一一对应
            CBUFFER_START(UnityPerMaterial)
                half4  _ColorLow;
                half4  _ColorHigh;
                float  _HeightMin;
                float  _HeightMax;
            CBUFFER_END

            // ---- 顶点着色器 ----
            Varyings vert(Attributes input)
            {
                Varyings output;

                // 🎓 坐标变换：Object Space → Clip Space（用于屏幕定位）
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);

                // 🎓 坐标变换：Object Space → World Space（用于高度读取）
                // TransformObjectToWorld 用的是 Model 矩阵（unity_ObjectToWorld）
                // 这个矩阵包含了物体的 Position、Rotation、Scale
                //
                // 为什么需要两次变换？
                //   positionHCS → GPU 用来决定像素画在屏幕哪个位置
                //   worldY      → 我们自己用来决定像素画什么颜色
                //   两个用途，两个目标空间
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.worldY = worldPos.y;

                return output;
            }

            // ---- 片段着色器 ----
            half4 frag(Varyings input) : SV_Target
            {
                // 🎓 saturate(x) = clamp(x, 0, 1)
                // 把高度归一化到 0~1 范围：
                //   Y <= HeightMin → 0（最暗）
                //   Y >= HeightMax → 1（最亮）
                //   中间值 → 线性插值
                //
                // 🎓 为什么用 saturate 而不是 clamp？
                //   saturate 在 GPU 上是免费的（硬件级别支持）
                //   clamp 可能多一条指令。这是 Shader 优化的常见技巧
                float t = saturate((input.worldY - _HeightMin) / (_HeightMax - _HeightMin));

                // 🎓 lerp = 线性插值 (Linear Interpolation)
                //   lerp(a, b, t) = a + (b - a) * t
                //   t=0 → 返回 _ColorLow（低处深色）
                //   t=1 → 返回 _ColorHigh（高处亮色）
                //   t=0.5 → 返回两个颜色的中间值
                //
                //   对比 2D：Godot 的 lerp()、Cocos 的 cc.misc.lerp() 完全一样
                //   在 Shader 中 lerp 是最常用的函数之一
                half4 color = lerp(_ColorLow, _ColorHigh, t);
                return color;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
