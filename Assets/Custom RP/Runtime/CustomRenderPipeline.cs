using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline {
	CameraRenderer renderer = new CameraRenderer();
	bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
	ShadowSettings shadowSettings;
	public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,bool useLightsPerObject, ShadowSettings shadowSettings) {
		this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
		this.shadowSettings = shadowSettings;
		this.useLightsPerObject = useLightsPerObject;
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
		GraphicsSettings.lightsUseLinearIntensity = true; //灯光使用线性强度
		InitializeForEditor();
	}
	
	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
		;
	}

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras) {
		for (int i = 0; i < cameras.Count; i++) {
			renderer.Render(context, cameras[i], useDynamicBatching, useGPUInstancing,useLightsPerObject,shadowSettings);
		}
	}

}