#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_DetailMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)//ST变量由UNITY赋值？
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

float2 TransformDetailUV (float2 detailUV) {
	float4 detailST = INPUT_PROP(_DetailMap_ST);
	return detailUV * detailST.xy + detailST.zw;
}

float4 GetDetail (float2 detailUV) {//detail贴图的强度
	float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUV);
	return map*2.0-1.0;
}

float4 GetMask (float2 baseUV){
    return SAMPLE_TEXTURE2D(_MaskMap,sampler_BaseMap,baseUV);
}

float GetFresnel (float2 baseUV) 
{
    return INPUT_PROP( _Fresnel);
}
float3 GetEmission (float2 baseUV) 
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
    float4 color = INPUT_PROP( _EmissionColor);
    return map.rgb * color.rgb;
}
float2 TransformBaseUV(float2 baseUV) 
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}
 
 //从缓冲区获取基础颜色，再采样贴图，将贴图颜色加上detail，最后乘以基础颜色 
float4 GetBase (float2 baseUV, float2 detailUV = 0.0) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	
	float4 detail = GetDetail(detailUV).r * INPUT_PROP(_DetailAlbedo);

	float mask = GetMask(baseUV).b;
	//只有R通道影响反照率，将其推向黑色或白色。这可以通过用0或1插值颜色来完成
	map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail)*mask);
	map.rgb *= map.rgb; //伽马空间与线性空间的转换

	return map * color;
}
 
float GetCutoff(float2 baseUV) 
{
    return INPUT_PROP(_Cutoff);
}
 
float GetMetallic(float2 baseUV) 
{
	float metallic = INPUT_PROP(_Metallic);
	metallic *= GetMask(baseUV).r;
	return metallic;
}
 
float GetSmoothness (float2 baseUV, float2 detailUV = 0.0) {
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(baseUV).a;

	float detail = GetDetail(detailUV).b * INPUT_PROP(_DetailSmoothness);
	float mask = GetMask(baseUV).b;
	smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
	
	return smoothness;
}

float GetOcclusion (float2 baseUV) {
	//当强度为1时，使用occlusion，当强度为0时，忽略occlusion
	//原代码似乎搞反了？
    float strength = INPUT_PROP(_Occlusion);
	float occlusion = GetMask(baseUV).g;
	occlusion = lerp(1.0,occlusion, strength);
	return occlusion;
}
#endif