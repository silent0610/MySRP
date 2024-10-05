#ifndef CUSTOM_CAMERA_RENDERER_PASSES_INCLUDED
#define CUSTOM_CAMERA_RENDERER_PASSES_INCLUDED

TEXTURE2D(_SourceTexture);

struct Varyings {	
	float4 positionCS_SS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV; 
	};

Varyings DefaultPassVertex (uint vertexID : SV_VertexID) {
	Varyings output;
	// 三角形三个顶点的位置,(-1,-1),(-1,3),(3,-1)
	output.positionCS_SS = float4(
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
	return SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, input.screenUV, 0);
}

#endif