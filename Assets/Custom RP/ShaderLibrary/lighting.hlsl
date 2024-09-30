#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

//计算入射光照
//计算投到该表面上的光（颜色，能量）
float3 IncomingLight (Surface surface, Light light) {
	return
		saturate(dot(surface.normal, light.direction) * light.attenuation) *
		light.color;
}

float3 GetLighting(Surface surface, BRDF brdf,Light light) {
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

//根据物体的表面信息获取最终光照结果
float3 GetLighting(Surface surfaceWS,BRDF brdf,GI gi) {
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;
    //计算间接光照
    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
    //对每个方向光源计算直接光照
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        color += GetLighting(surfaceWS,brdf, light);
    }
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; min(unity_LightData.y, 8); j++) {
			//存储了该点可见的光源索引，在启用映射后，得到了自定义的索引
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++) {
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);

		}
	#endif
    return color;
}

#endif