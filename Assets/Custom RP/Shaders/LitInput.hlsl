#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_DetailMap);
SAMPLER(sampler_DetailMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_DetailNormalMap);


UNITY_INSTANCING_BUFFER_START(UnityPerMaterial) //shader(material)属性值由实例化缓冲区传递到GPU
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
UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig {
	Fragment fragment;
	float2 baseUV;
	float2 detailUV;
	bool useMask;
	bool useDetail;

};

float GetFinalAlpha (float alpha) {
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

InputConfig GetInputConfig (float4 positionSS,float2 baseUV, float2 detailUV = 0.0) {
	InputConfig c;
	c.fragment = GetFragment(positionSS);
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	c.useMask = false;
	c.useDetail = false;
	return c;
}
float4 GetMask (InputConfig c){
	if (c.useMask) {
		return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, c.baseUV);
	}
	return 1.0;
}

//计算切线空间的法线
float3 GetNormalTS (InputConfig c) {
	//采样法线贴图
	float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, c.baseUV);
	float scale = INPUT_PROP(_NormalScale);
	float3 normal = DecodeNormal(map, scale);
	//细节法线贴图
	if (c.useDetail) {
		map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, c.detailUV);
		scale = INPUT_PROP(_DetailNormalScale) * GetMask(c).b;//应用遮罩
		float3 detail = DecodeNormal(map, scale);
		normal = BlendNormalRNM(normal, detail);//结合法线，围绕基础法线旋转细节法线
	}
	return normal;
}

//输入空间的法线（法线贴图），世界空间表面法线，世界空间切线，返回应用了贴图的世界空间法线
float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
	//TBN矩阵
	float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}
float2 TransformDetailUV (float2 detailUV) {
	float4 detailST = INPUT_PROP(_DetailMap_ST);
	return detailUV * detailST.xy + detailST.zw;
}

float4 GetDetail (InputConfig c) {//detail贴图的强度
	if (c.useDetail) {
		float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, c.detailUV);
		return map * 2.0 - 1.0;
	}
	return 0.0;
}



float GetFresnel (InputConfig c) 
{
    return INPUT_PROP( _Fresnel);
}
float3 GetEmission (InputConfig c) 
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, c.baseUV);
    float4 color = INPUT_PROP( _EmissionColor);
    return map.rgb * color.rgb;
}
float2 TransformBaseUV(float2 baseUV) 
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}
 
 //从缓冲区获取基础颜色，再采样贴图，将贴图颜色加上detail，最后乘以基础颜色 
float4 GetBase (InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	
	if (c.useDetail) {
		float4 detail = GetDetail(c).r * INPUT_PROP(_DetailAlbedo);
		float mask = GetMask(c).b;
		//只有R通道影响反照率，将其推向黑色或白色。这可以通过用0或1插值颜色来完成
		map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail)*mask);
		map.rgb *= map.rgb; //伽马空间与线性空间的转换
	}
	return map * color;
}
 
float GetCutoff(InputConfig c) 
{
    return INPUT_PROP(_Cutoff);
}
 
float GetMetallic(InputConfig c) 
{
	float metallic = INPUT_PROP(_Metallic);
	metallic *= GetMask(c).r;
	return metallic;
}
 
float GetSmoothness (InputConfig c) {
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(c).a;
	if (c.useDetail) {
		float detail = GetDetail(c).b * INPUT_PROP(_DetailSmoothness);
		float mask = GetMask(c).b;
		smoothness =
			lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
	}
	return smoothness;
}

float GetOcclusion (InputConfig c) {
	//当强度为1时，使用occlusion，当强度为0时，忽略occlusion
	//原代码似乎搞反了？
    float strength = INPUT_PROP(_Occlusion);
	float occlusion = GetMask(c).g;
	occlusion = lerp(1.0,occlusion, strength);
	return occlusion;
}
#endif