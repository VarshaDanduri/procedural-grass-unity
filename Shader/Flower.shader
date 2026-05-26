Shader "Grass/Flower" {
    Properties {
        _MainTex("Flower Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _Tint("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader {
        Tags{"RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest"}
        Pass {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}
            Cull Off

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Flower.hlsl"
            ENDHLSL
        }
    }
}