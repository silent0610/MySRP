#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

float4 _PostFXSource_TexelSize; //Unity给出的屏幕像素大小
float4 GetSourceTexelSize () {
	return _PostFXSource_TexelSize;
}
TEXTURE2D(_PostFXSource);//中间帧缓冲区的数据
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_linear_clamp);

float4 GetSourceBicubic (float2 screenUV) {
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0
	);
}

float4 GetSource(float2 screenUV) {
	//LOD允许你手动指定要使用的 mipmap 级别。添加一个额外的参数来强制选择mip贴图级别为零来避开自动mip贴图选择。
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}
float4 GetSource2(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}
Varyings DefaultPassVertex (uint vertexID : SV_VertexID) {
	Varyings output;
	// 三角形三个顶点的位置,(-1,-1),(-1,3),(3,-1)
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2( // 三角形三个顶点的UV, (0,0),(0,2),(2,0)
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	if (_ProjectionParams.x < 0.0) { //可能因为图形api的区别,导致从左上角开始,需要翻转
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;

}
float4 CopyPassFragment (Varyings input) : SV_TARGET {
	return GetSource(input.screenUV);
}


float4 BloomHorizontalPassFragment (Varyings input) : SV_TARGET {
	float3 color = 0.0;
	float offsets[] = {
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++) {
		//在滤波的同时进行下采样,所以乘2.
		//什么是下采样,即图片尺寸的缩小
		//由于上一张纹理的尺寸是两倍,所以这里的偏移也是两倍
		//要对上一张纹理采样,而上一张纹理的像素尺寸是当前纹理的两倍,所以是2.
		// 4.5 => 9
		float offset = offsets[i]  * 2 * GetSourceTexelSize().x; 
		color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

float4 BloomVerticalPassFragment (Varyings input) : SV_TARGET {
	float3 color = 0.0;
	//适当偏移
	//原理是什么,为什么要这样偏移
	//选择的偏移量和权重经过优化，能够模拟高斯分布，
	//从而在视觉上提供平滑的模糊效果。这些特定的偏移量和权重经过测试，能有效保留模糊效果的质量?
	float offsets[] = {
		-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
		};
	float weights[] = {
		0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	};
	for (int i = 0; i < 5; i++) {
		//在水平采样时已经进行了下采样
		//本次是在相同尺寸下进行滤波,上一张纹理的像素尺寸的纹理和当前纹理的像素尺寸相同,故不需要乘2
		float offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

bool _BloomBicubicUpsampling;

float _BloomIntensity;
float4 BloomCombinePassFragment (Varyings input) : SV_TARGET {
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lowRes * _BloomIntensity + highRes, 1.0);
}

//threshold
float4 _BloomThreshold;

float3 ApplyBloomThreshold (float3 color) {
	float brightness = Max3(color.r, color.g, color.b);
	float soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	float contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}

float4 BloomPrefilterPassFragment (Varyings input) : SV_TARGET {
	float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return float4(color, 1.0);
}
#endif
