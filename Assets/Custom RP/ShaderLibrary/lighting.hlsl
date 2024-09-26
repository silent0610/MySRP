#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

//计算投到该表面上的光（颜色，能量）
float3 IncomingLight (Surface surface, Light light) {
	return
		saturate(dot(surface.normal, light.direction) * light.attenuation) *
		light.color;
}

float3 GetLighting(Surface surface, BRDF brdf,Light light) {
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}


float3 GetLighting(Surface surfaceWS,BRDF brdf,GI gi) {
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;
    //计算间接光照
    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
    //对每个光源计算直接光照
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        color += GetLighting(surfaceWS,brdf, light);
    }
    return color;
}

#endif