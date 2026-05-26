#ifndef FLOWER_INCLUDED
#define FLOWER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct DrawVertex {
    float3 positionWS;
    float2 uv;
    float windStrength;
};
struct DrawTriangle {
    float3 lightingNormalWS;
    DrawVertex vertices[3];
};
StructuredBuffer<DrawTriangle> _DrawTriangles;

struct VertexOutput {
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float windStrength : TEXCOORD3;
    float4 positionCS : SV_POSITION;
};

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
float4 _Tint;
float _Cutoff;

VertexOutput Vertex(uint vertexID : SV_VertexID) {
    VertexOutput output = (VertexOutput)0;
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    DrawVertex v = tri.vertices[vertexID % 3];
    output.positionWS = v.positionWS;
    output.normalWS = tri.lightingNormalWS;
    output.uv = v.uv;
    output.windStrength = v.windStrength;
    output.positionCS = TransformWorldToHClip(v.positionWS);
    return output;
}

half4 Fragment(VertexOutput input) : SV_Target {
    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    clip(tex.a - _Cutoff); // discard transparent pixels

    float3 albedo = tex.rgb * _Tint.rgb;
    // Darken slightly with wind, like the grass
    albedo *= lerp(1.0, 0.7, input.windStrength);

    // Simple lighting
    Light mainLight = GetMainLight();
    float NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
    float3 finalColor = albedo * (NdotL * mainLight.color.rgb + 0.3);

    return half4(finalColor, 1);
}

#endif