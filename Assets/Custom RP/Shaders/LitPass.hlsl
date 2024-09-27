#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"





struct Attributes {
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings {
    float4 positionCS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float2 baseUV : VAR_BASE_UV;
    float2 detailUV : VAR_DETAIL_UV;
    GI_VARYINGS_DATA
    float3 normalWS : VAR_NORMAL;
    float4 tangentWS : VAR_TANGENT;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
Varyings LitPassVertex(Attributes input) {
    Varyings output;
    //传递GPU实例化ID
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    // 传递 GI_DATA
    TRANSFER_GI_DATA(input, output);
    //计算位置
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    //贴图位置
    output.baseUV = TransformBaseUV(input.baseUV);
    output.detailUV = TransformDetailUV(input.baseUV);
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET {
    
    UNITY_SETUP_INSTANCE_ID(input);
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    
    float4 base = GetBase(input.baseUV, input.detailUV);//贴图颜色乘以基础颜色+detail

    Surface surface;
    surface.position = input.positionWS; //世界坐标
    surface.normal = normalize(input.normalWS); //表面法线
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV, input.detailUV);
    surface.fresnelStrength = GetFresnel(input.baseUV);
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    surface.occlusion = GetOcclusion(input.baseUV);
    surface.normal = NormalTangentToWorld(GetNormalTS(input.baseUV), input.normalWS, input.tangentWS);
    //为什么不归一化？大多数网格的法线不会像三角形的顶点法线一样弯曲太多？？为什么？
    surface.interpolatedNormal = input.normalWS;
    BRDF brdf;
    #if defined(_PREMULTIPLY_ALPHA)
        brdf = GetBRDF(surface, true);
    #else
        brdf = GetBRDF(surface);
    #endif
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(surface, brdf, gi); //着色，包括直接光，间接光
    color += GetEmission(input.baseUV);
    
    return float4(color, surface.alpha);
}



#endif