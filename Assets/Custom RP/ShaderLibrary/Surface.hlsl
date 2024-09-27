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
	float occlusion; //遮挡,遮挡，即间接光照射不到，只应用于间接光照。理解：当光源直接照射缝隙时，遮挡是不生效的，因为被照亮了。
};

#endif