#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64
CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

    // Other lights 数量,颜色,位置
	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light {
    float3 color;
    float3 direction;
    float attenuation;
};

int GetDirectionalLightCount() {
    return _DirectionalLightCount;
}
int GetOtherLightCount () {
	return _OtherLightCount;
}
float FadedShadowStrength(float distance, float scale, float fade) {
    return saturate((1.0 - distance * scale) * fade);
}
//暂时不清楚这个函数的作用
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
    if (i == _CascadeCount) {
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
DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData) {
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x ;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
    return data;
}
Light GetDirectionalLight(int index, Surface surfaceWS, ShadowData shadowData) {
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);

    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
    
    return light;
}

//获得从lighting.cs中赋值的其他光源
Light GetOtherLight(int index, Surface surfaceWS,ShadowData shadowData) {
    Light light;
    light.color = _OtherLightColors[index].rgb;
    //光源到表面的方向,距离
    float3 ray = _OtherLightPositions[index].xyz - surfaceWS.position;
    light.direction = normalize(ray);
    float distanceSqr = max(dot(ray, ray), 0.00001);
	float rangeAttenuation = Square(
		saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w))
	);
	light.attenuation = rangeAttenuation / distanceSqr;

    return light;
}

#endif