#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
//���ʹ�õ���PCF 3X3
#if defined(_DIRECTIONAL_PCF3)
//��Ҫ4���˲�����
#define DIRECTIONAL_FILTER_SAMPLES 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
#define DIRECTIONAL_FILTER_SAMPLES 9
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
#define DIRECTIONAL_FILTER_SAMPLES 16
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4



TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4 _ShadowDistanceFade;
	float4 _ShadowAtlasSize;
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT* MAX_CASCADE_COUNT];
CBUFFER_END

struct DirectionalShadowData {
    float strength;
    int tileIndex;
	float normalBias;
};

float SampleDirectionalShadowAtlas(float3 positionSTS) {
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

struct ShadowMask
{
    bool distance;
    float4 shadows;
};
struct ShadowData {
    int cascadeIndex;
    float strength;
    float cascadeBlend;
    ShadowMask shadowMask;
};
float FilterDirectionalShadow(float3 positionSTS) 
    {
        #if defined(DIRECTIONAL_FILTER_SETUP)
        
        float weights[DIRECTIONAL_FILTER_SAMPLES];
        
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.yyxx;
        DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        float shadow = 0;
        for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) 
        {
            
            shadow += weights[i] * SampleDirectionalShadowAtlas(
            float3(positions[i].xy, positionSTS.z)
        );
    }
    return shadow;
#else
    return SampleDirectionalShadowAtlas(positionSTS);
#endif
}
float GetCascadedShadow(DirectionalShadowData directional, ShadowData global, Surface surfaceWS) 
{
    //计算法线偏差
    float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
          
    float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);
        
    if (global.cascadeBlend < 1.0) 
    {
        normalBias = surfaceWS.normal *(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
    }
    return shadow;
}

//得到烘焙阴影的衰减值
float GetBakedShadow(ShadowMask mask) 
{
    float shadow = 1.0;
    if (mask.distance) 
    {
        shadow = mask.shadows.r;
    }
    return shadow;
}

float GetBakedShadow(ShadowMask mask, float strength) 
{
    if (mask.distance) 
    {
        return lerp(1.0, GetBakedShadow(mask), strength);
    }
    return 1.0;
}
//混合烘焙和实时阴影
float MixBakedAndRealtimeShadows(ShadowData global, float shadow, float strength) 
{
    float baked = GetBakedShadow(global.shadowMask);
    if (global.shadowMask.distance) 
    {
        shadow = lerp(baked, shadow, global.strength);
        return lerp(1.0, shadow, strength);
    }
    return lerp(1.0, shadow, strength);
}

//计算阴影衰减
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS) 
{
  #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
  #endif
    float shadow;
    if (directional.strength * global.strength <= 0.0)
    {
        shadow = GetBakedShadow(global.shadowMask, abs(directional.strength));
    }
    else 
    {
        shadow = GetCascadedShadow(directional, global, surfaceWS);             
        shadow = MixBakedAndRealtimeShadows(global, shadow, directional.strength);  
    }       
    return shadow;
}

#endif