#ifndef CUSTOM_CAMERA_RENDERER_PASSES_INCLUDED
#define CUSTOM_CAMERA_RENDERER_PASSES_INCLUDED

TEXTURE2D(_SourceTexture);

struct Varyings { … };

Varyings DefaultPassVertex (uint vertexID : SV_VertexID) { … }

float4 CopyPassFragment (Varyings input) : SV_TARGET {
	return SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, input.screenUV, 0);
}

#endif