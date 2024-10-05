#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

//get TransformObjectToWorld TransformWorldToHClip via package
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_DISTANCE) || defined(_SHADOW_MASK_DISTANCE)
  #define SHADOWS_SHADOWMASK
#endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp); //这些采样器都是预先定义好的,在这里只是声明
SAMPLER(sampler_point_clamp);

bool IsOrthographicCamera () { //判断是否是正交相机
	return unity_OrthoParams.w; //存储是否正交,是为1
}
//将正交相机的的深度值转为视图空间深度
float OrthographicDepthBufferToLinear (float rawDepth) {
	#if UNITY_REVERSED_Z //即越远越小
		rawDepth = 1.0 - rawDepth;
	#endif
	return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}
#include "Fragment.hlsl"

float3 DecodeNormal(float4 sample,float scale){
    #if defined(UNITY_NO_DXT5nm) 
        return normalize(UnpackNormalRGB(sample,scale));
    #else
        return normalize(UnpackNormalmapRGorAG(sample,scale));
    #endif
}
float Square(float v) {
    return v * v;
}
float DistanceSquared(float3 pA, float3 pB) {
	return dot(pA - pB, pA - pB);
}

void ClipLOD(Fragment fragment,float fade){
    #if defined(LOD_FADE_CROSSFADE)
        float dither = InterleavedGradientNoise(fragment.positionSS, 0);
        clip(fade + (fade < 0.0 ? dither : -dither));
    #endif 

}


#endif