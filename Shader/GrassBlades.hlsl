// MIT License

// Copyright (c) 2020 NedMakesGames

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// Make sure this file is not included twice
#ifndef GRASSBLADES_INCLUDED
#define GRASSBLADES_INCLUDED

// Include some helper functions
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "NMGGrassBladeGraphicsHelpers.hlsl"

// This describes a vertex on the generated mesh
struct DrawVertex {
    float3 positionWS; // The position in world space
    float height; // The height of this vertex on the grass blade
    float windStrength;
};
// A triangle on the generated mesh
struct DrawTriangle {
    float3 lightingNormalWS; // A normal, in world space, to use in the lighting algorithm
    DrawVertex vertices[3]; // The three points on the triangle
};
// A buffer containing the generated mesh
StructuredBuffer<DrawTriangle> _DrawTriangles;

struct VertexOutput {
    float uv            : TEXCOORD0; // The height of this vertex on the grass blade
    float3 positionWS   : TEXCOORD1; // Position in world space
    float3 normalWS     : TEXCOORD2; // Normal vector in world space
    float windStrength : TEXCOORD3;

    float4 positionCS   : SV_POSITION; // Position in clip space
};

// Properties
float4 _BaseColor;
float4 _TipColor;
float4 _ShadowColor;
float _ShadowTintStrength;


// Vertex functions

VertexOutput Vertex(uint vertexID: SV_VertexID) {
    // Initialize the output struct
    VertexOutput output = (VertexOutput)0;

    // Get the vertex from the buffer
    // Since the buffer is structured in triangles, we need to divide the vertexID by three
    // to get the triangle, and then modulo by 3 to get the vertex on the triangle
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    DrawVertex input = tri.vertices[vertexID % 3];

    output.positionWS = input.positionWS;
    output.normalWS = tri.lightingNormalWS;
    output.uv = input.height;
    output.positionCS = TransformWorldToHClip(input.positionWS);
    output.windStrength = input.windStrength;

    return output;
}

// Fragment functions

half4 Fragment(VertexOutput input) : SV_Target {
    float3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, input.uv);
    albedo *= lerp(1.0, 0.5, input.windStrength*0.5);
    float3 normalWS = normalize(input.normalWS);

    // Main directional light (with shadows)
    float4 shadowCoord = CalculateShadowCoord(input.positionWS, input.positionCS);
    Light mainLight = GetMainLight(shadowCoord);

    // How much the surface faces the light, multiplied by the shadow map
    float NdotL = saturate(dot(normalWS, mainLight.direction));
    float lightAmount = NdotL * mainLight.shadowAttenuation;

    // Darken toward the base of each blade (input.uv is 0 at base, 1 at tip)
    lightAmount *= input.uv;

    // Lit color: blade's color hit by the sun
    float3 litColor = albedo * mainLight.color.rgb;

    // Shadowed color: blend the blade's color toward the shadow tint
    float3 shadowedColor = lerp(albedo, _ShadowColor.rgb, _ShadowTintStrength);

    // Pick between lit and shadowed based on how much light hits this pixel
    float3 finalColor = lerp(shadowedColor, litColor, lightAmount);

    // Add a small ambient term so even shadowed parts get some sky bounce
    finalColor += albedo * SampleSH(normalWS) * 0.3;

    return half4(finalColor, 1);
}

#endif