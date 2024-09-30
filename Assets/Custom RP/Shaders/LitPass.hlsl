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
	#if defined(_DETAIL_MAP)
		float2 detailUV : VAR_DETAIL_UV;
	#endif
    GI_VARYINGS_DATA
    float3 normalWS : VAR_NORMAL;
	#if defined(_NORMAL_MAP)
		float4 tangentWS : VAR_TANGENT;
	#endif
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

    #if defined(_NORMAL_MAP)
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    #endif
    #if defined(_DETAIL_MAP)
		output.detailUV = TransformDetailUV(input.baseUV);
	#endif
    return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET {
    
    UNITY_SETUP_INSTANCE_ID(input);
    ClipLOD(input.positionCS.xy, unity_LODFade.x); //LOD裁剪
    InputConfig config = GetInputConfig(input.baseUV);
    // 是否使用sMASK，detailmap，裁剪等
    #if defined(_MASK_MAP)
		config.useMask = true;
	#endif
    #if defined(_DETAIL_MAP)
		config.detailUV = input.detailUV;
		config.useDetail = true;
	#endif
    float4 base = GetBase(config);//贴图颜色乘以基础颜色+detail
    #if defined(_CLIPPING) //透明度裁剪
		clip(base.a - GetCutoff(config));
	#endif
    Surface surface;
    surface.position = input.positionWS; //世界坐标
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);
    surface.fresnelStrength = GetFresnel(config);
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    surface.occlusion = GetOcclusion(config);
    //是否使用法线贴图
    #if defined(_NORMAL_MAP)
    surface.normal = NormalTangentToWorld(GetNormalTS(config),input.normalWS,input.tangentWS);//切线空间转表面法线
    surface.interpolatedNormal = input.normalWS;//为什么不归一化？大多数网格的法线不会像三角形的顶点法线一样弯曲太多？？为什么？
    #else
    	surface.normal = normalize(input.normalWS);
		surface.interpolatedNormal = surface.normal;
    #endif

    BRDF brdf;
    #if defined(_PREMULTIPLY_ALPHA)
        brdf = GetBRDF(surface, true);
    #else
        brdf = GetBRDF(surface);
    #endif
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(surface, brdf, gi); //着色
    color += GetEmission(config);
    
    return float4(color, surface.alpha);
}



#endif