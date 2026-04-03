// ============================================================
// MinimapMarker.shader — 小地图角色标记着色器（纯色）
// ============================================================
//
// 用途：在小地图上把玩家/敌人渲染成醒目的纯色方块
//       玩家 → 绿色    敌人 → 红色
//
// 🎓 为什么不和 MinimapFlat 共用一个 Shader？
//   虽然可以加个开关（keyword）来切换模式，但分成两个 Shader
//   更清晰：各自有独立的 Material，在 Renderer Feature 中
//   通过 Layer 过滤分别应用。代码简单，维护方便。
//   实际项目中，"一个 Shader 做一件事"是常见的设计原则。
//
// 🎓 技巧：ZTest Always + Queue 靠后
//   标记要"浮"在地形上面，不被地形遮挡
//   ZTest Always = 不做深度测试，无论前后都画
//   Queue = Overlay 保证在所有 Geometry 之后渲染
// ============================================================

Shader "Custom/MinimapMarker"
{
    Properties
    {
        _MarkerColor ("标记颜色", Color) = (1, 0, 0, 1)
        // 敌人用红色 (1,0,0)，玩家用绿色 (0,1,0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+100"
            // 🎓 Queue = Geometry+100
            //   比普通几何体晚渲染（数字越大越晚）
            //   这样标记会画在地形之上
            //   对比：Geometry=2000, Geometry+100=2100
        }

        Pass
        {
            Name "MinimapMarker"
            Lighting Off
            Fog { Mode Off }

            // 🎓 ZTest Always：关闭深度测试
            // 正常情况下，GPU 会检查每个像素的深度值：
            //   如果新像素比已有像素更远 → 不画（被遮挡）
            //   如果新像素更近 → 画上去
            // ZTest Always = 无论深度如何都画上去
            // 效果：角色标记永远不会被地形遮挡
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _MarkerColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _MarkerColor;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
