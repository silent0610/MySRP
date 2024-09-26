#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position; //世界位置
	float3 normal;  //世界空间法线
	float3 viewDirection;//相机到表面的方向
	float3 color;  //表面基础颜色（采样了贴图
	float alpha;
	float metallic;
	float smoothness;
	float depth;
	float dither;
	float fresnelStrength;//菲涅尔反射强度，最后乘，控制整体强度

};

#endif