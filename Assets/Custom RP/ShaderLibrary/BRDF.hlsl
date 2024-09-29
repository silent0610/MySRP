#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED
#define MIN_REFLECTIVITY 0.04


float OneMinusReflectivity(float metallic) {
    float range = 1.0 - MIN_REFLECTIVITY;
    return range * (1 - metallic);
}
//BRDF包括漫反射部分颜色和镜面反射部分颜色，(根据物理属性计算)
struct BRDF {
    float3 diffuse;
    float3 specular;
    float roughness;
    float perceptualRoughness;
    float fresnel;
};
//基于BRDF的间接照明
float3 IndirectBRDF (Surface surface, BRDF brdf, float3 diffuse, float3 specular) 
{
    float fresnelStrength =surface.fresnelStrength * Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));

    float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);//根据强度在BRDF镜面颜色和菲涅耳颜色之间进行插值
    reflection /= (brdf.roughness * brdf.roughness + 1.0);
    return (diffuse * brdf.diffuse + reflection)* surface.occlusion;;
}
//获取给定表面的BRDF数据,使用表面的属性计算BRDF
BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false) {
    BRDF brdf;
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.diffuse = surface.color * oneMinusReflectivity; //根据金属度计算表面的BRDF漫反射光能量（颜色）
    
    //预乘alpha
    if (applyAlphaToDiffuse) {
        brdf.diffuse *= surface.alpha;
    }
    
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);//表面的BRDF高光能量（颜色）
   
   //光滑度转实际粗糙度
    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    
    brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);//表面的菲涅尔反射强度
    return brdf;
}

float SpecularStrength(Surface surface, BRDF brdf, Light light) {
    float3 h = SafeNormalize(light.direction + surface.viewDirection);
    float nh2 = Square(saturate(dot(surface.normal, h)));
    float lh2 = Square(saturate(dot(light.direction, h)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2) * normalization);
}

float3 DirectBRDF(Surface surface, BRDF brdf, Light light) {
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}
#endif