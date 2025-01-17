#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
	#define OTHER_FILTER_SAMPLES 4
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
	#define OTHER_FILTER_SAMPLES 9
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
	#define OTHER_FILTER_SAMPLES 16
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4



TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
int _CascadeCount;
float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
float4 _CascadeData[MAX_CASCADE_COUNT];
float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
float4 _ShadowDistanceFade;
float4 _ShadowAtlasSize;
float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
CBUFFER_END

//当前方向光的阴影数据
struct DirectionalShadowData {
    float strength; //光源控制的当前阴影的强度,可以用于确定我们是否可以跳过采样实时阴影
    int tileIndex;//图块便宜
    float normalBias;//法线偏差
    int shadowMaskChannel;
};
struct OtherShadowData {
    float strength;//光源控制的当前阴影的强度
    int tileIndex;
    bool isPoint;
    int shadowMaskChannel;
    float3 lightPositionWS;
    float3 lightDirectionWS;
	float3 spotDirectionWS;

};
//存储烘焙阴影数据
struct ShadowMask {
    bool always;//标记了 showMask 模式
    bool distance;// 标记 是否启用 Distance Shadow Mask
    float4 shadows;//存储烘焙的阴影数据。

};
//表面的阴影数据
struct ShadowData {
    int cascadeIndex;
    float strength;//ShadowData结构体中添加一个字段Strength作为一个标识符，如果超出最后一个级联范围就设为0。
    float cascadeBlend;
    ShadowMask shadowMask;
};

float FadedShadowStrength(float distance, float scale, float fade) {
    return saturate((1.0 - distance * scale) * fade);
}
//得到世界空间的表面阴影数据,比如控制阴影过渡,级联强度
ShadowData GetShadowData(Surface surfaceWS) {
    ShadowData data;
    data.cascadeBlend = 1.0;
    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
    data.strength = FadedShadowStrength(
        surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y
    );
    int i;
    for (i = 0; i < _CascadeCount; i++) {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w) {
            //公式计算阴影过渡时的强度
            float fade = FadedShadowStrength(
                distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
            );
            if (i == _CascadeCount - 1) {
                data.strength *= fade;
            } else {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    //如果超出最大级联范围且级联数量大于0，将全局阴影强度设为0(不进行阴影采样)  
    if (i == _CascadeCount && _CascadeCount > 0) {
        data.strength = 0.0;
    }
    #if defined(_CASCADE_BLEND_DITHER)
    else if (data.cascadeBlend < surfaceWS.dither) 
    {
        i += 1;
    }
    #endif
    #if !defined(_CASCADE_BLEND_SOFT)
		data.cascadeBlend = 1.0;
	#endif
    data.cascadeIndex = i;
    return data;
}


//得到烘焙阴影的衰减值
float GetBakedShadow(ShadowMask mask, int channel) {
    float shadow = 1.0;
    if (mask.distance || mask.always) {
        if (channel >= 0) {
            shadow = mask.shadows[channel];
        }
    }
    return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel, float strength) {
    if (mask.distance || mask.always) {
        return lerp(1.0, GetBakedShadow(mask, channel), strength);
    }
    return 1.0;
}

//采样阴影图集
float SampleDirectionalShadowAtlas(float3 positionSTS) {
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}
float SampleOtherShadowAtlas (float3 positionSTS, float3 bounds) {
	positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
    return SAMPLE_TEXTURE2D_SHADOW(
		_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

float FilterDirectionalShadow(float3 positionSTS) {//利用卷积核采样阴影,以减少锯齿
    #if defined(DIRECTIONAL_FILTER_SETUP)
        
        float weights[DIRECTIONAL_FILTER_SAMPLES];
        
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.yyxx;
        DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        float shadow = 0;
        for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
            
            shadow += weights[i] * SampleDirectionalShadowAtlas(
                float3(positions[i].xy, positionSTS.z)
            );
        }
        return shadow;
    #else
        return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}
float FilterOtherShadow (float3 positionSTS, float3 bounds) {
	#if defined(OTHER_FILTER_SETUP)
		real weights[OTHER_FILTER_SAMPLES];
		real2 positions[OTHER_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.wwzz;
		OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
			shadow += weights[i] * SampleOtherShadowAtlas(
				float3(positions[i].xy, positionSTS.z), bounds
			);
		}
		return shadow;
	#else
		return SampleOtherShadowAtlas(positionSTS, bounds);
	#endif
}
//实时阴影采样
float GetCascadedShadow(DirectionalShadowData directional, ShadowData global, Surface surfaceWS) {
    //计算法线偏差
    float3 normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    
    float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);
    
    if (global.cascadeBlend < 1.0) {
        normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
    }
    return shadow;
}
static const float3 pointShadowPlanes[6] = {
	float3(-1.0, 0.0, 0.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, -1.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, 0.0, -1.0),
	float3(0.0, 0.0, 1.0)
};
float GetOtherShadow (
	OtherShadowData other, ShadowData global, Surface surfaceWS) {
	float tileIndex = other.tileIndex;
	float3 lightPlane = other.spotDirectionWS;
    if (other.isPoint) {
		float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
		tileIndex += faceOffset;
        lightPlane = pointShadowPlanes[faceOffset];
	}   
    float4 tileData = _OtherShadowTiles[tileIndex];
    //光到着色点的距离
    //光到着色点的距离向量点乘光中心方向,即|a||b|cos
    float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
	float distanceToLightPlane = dot(surfaceToLight,lightPlane);
    float3 normalBias = surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);

	float4 positionSTS = mul(
		_OtherShadowMatrices[tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);//透视投影，变换位置的XYZ除以Z
}

//混合烘焙和实时阴影
float MixBakedAndRealtimeShadows(
    ShadowData global, float shadow, int shadowMaskChannel, float strength
) {
    float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
    if (global.shadowMask.always) {
        shadow = lerp(1.0, shadow, global.strength);
        shadow = min(baked, shadow);
        return lerp(1.0, shadow, strength);
    }
    if (global.shadowMask.distance) {
        shadow = lerp(baked, shadow, global.strength);
        return lerp(1.0, shadow, strength);
    }
    return lerp(1.0, shadow, strength * global.strength);
}

//计算最终阴影衰减
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS) {
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif
    float shadow;
    if (directional.strength * global.strength <= 0.0) {
        shadow = GetBakedShadow(global.shadowMask, directional.shadowMaskChannel, abs(directional.strength));
    } else {
        shadow = GetCascadedShadow(directional, global, surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global, shadow, directional.shadowMaskChannel, directional.strength);
    }
    return shadow;
}
//得到其他类型光源的阴影衰减,光源和烘焙的阴影遮罩
//光源,全局的阴影强度用来确定是否可以跳过实时阴影的采样，可能因为我们超出了最大阴影距离或在级联包围球之外
float GetOtherShadowAttenuation(OtherShadowData other, ShadowData global, Surface surfaceWS) {
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif
    
    float shadow;
    if (other.strength * global.strength <= 0.0) {//当光源阴影强度设为0,或者全局强度小于0(即超过阴影过渡范围)时,采样shadowmask
        shadow = GetBakedShadow(global.shadowMask, other.shadowMaskChannel, abs(other.strength) );
    } else {
		shadow = GetOtherShadow(other, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(
			global, shadow, other.shadowMaskChannel, other.strength
		);
    }
    return shadow;
}

#endif