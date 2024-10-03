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
	protected override RenderPipeline CreatePipeline() {
		return new CustomRenderPipeline(allowHDR, useDynamicBatching, useGPUInstancing, useLightsPerObject, useSRPBatcher, shadows, postFXSettings);
	}
}