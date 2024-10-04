using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset {
	[SerializeField]
	bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true, useLightsPerObject = true;
	[SerializeField]
	ShadowSettings shadows = default;
	[SerializeField]
	PostFXSettings postFXSettings = default;
	[SerializeField]//控制HDR
	bool allowHDR = true;

	public enum ColorLUTResolution {_16 = 16,_32 = 32,_64 = 64 };
	[SerializeField]
	ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
	
	protected override RenderPipeline CreatePipeline() {
		return new CustomRenderPipeline(allowHDR, useDynamicBatching, useGPUInstancing, 
			useLightsPerObject, useSRPBatcher, shadows, postFXSettings, (int)colorLUTResolution);
	}
}