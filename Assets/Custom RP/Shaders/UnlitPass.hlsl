#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes {
	float3 positionOS : POSITION;
	float4 color: COLOR; //顶点色
	#if defined(_FLIPBOOK_BLENDING)
		float4 baseUV : TEXCOORD0; //baseuv1+2 放在一个向量中?
		float flipbookBlend : TEXCOORD1; //混合系数
	#else
		float2 baseUV : TEXCOORD0;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//在顶点函数中,SV_POSITION 表示顶点的剪辑空间位置,四维齐次坐标
//但是在fragment函数中 SV_POSITION 表示片段的屏幕空间坐标
struct Varyings {
	float4 positionCS_SS : SV_POSITION; //片元的屏幕空间坐标,空间转换由GPU执行
	#if defined(_VERTEX_COLORS)
		float4 color : VAR_COLOR;
	#endif
    #if defined(_FLIPBOOK_BLENDING) 
		float3 flipbookUVB:VAR_FLIPBOOK;
	#endif
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	#if defined(_VERTEX_COLORS)
		output.color = input.color;
	#endif
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(positionWS);
	output.baseUV.xy = TransformBaseUV(input.baseUV.xy);
	#if defined(_FLIPBOOK_BLENDING)
		output.flipbookUVB.xy = TransformBaseUV(input.baseUV.zw);
		output.flipbookUVB.z = input.flipbookBlend;
	#endif
	return output;
}

float4 UnlitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);

	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	#if defined(_VERTEX_COLORS)
		config.color = input.color;
	#endif
	#if defined(_FLIPBOOK_BLENDING)
		config.flipbookUVB = input.flipbookUVB;
		config.flipbookBlending = true;
	#endif

	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif
	return float4(float3(1,1,1), GetFinalAlpha(base.a));
	return float4(base.rgb, GetFinalAlpha(base.a));
}

#endif