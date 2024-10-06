#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED


TEXTURE2D(_CameraDepthTexture);

struct Fragment {
	float2 positionSS;
	float2 screenUV;
	float depth;//片元对应深度
	float bufferDepth;//存储在深度缓冲区的深度
	//两者可能不同,当前片元深度可能大于,也可能小于深度缓冲区的深度,相减可以得到深度差,即距离
}; 

Fragment GetFragment (float4 positionSS) {
	Fragment f;
	f.positionSS = positionSS.xy;
	f.screenUV = f.positionSS / _ScreenParams.xy; //屏幕坐标除以屏幕尺寸(以像素为单位)
	f.depth = IsOrthographicCamera() ?
		OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w;
	f.bufferDepth =
		SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, f.screenUV, 0);
	f.bufferDepth = IsOrthographicCamera() ?
		OrthographicDepthBufferToLinear(f.bufferDepth) :
		LinearEyeDepth(f.bufferDepth, _ZBufferParams);
	return f;
}

#endif