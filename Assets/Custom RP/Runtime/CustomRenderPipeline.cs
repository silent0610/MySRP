using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline {
	CameraRenderer renderer;
	bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
	ShadowSettings shadowSettings;
	PostFXSettings postFXSettings;
	bool allowHDR;
	int colorLUTResolution;
	public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, 
	bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, 
		ShadowSettings shadowSettings, PostFXSettings postFXSettings,
		int colorLUTResolution,Shader cameraRendererShader) 
	{
        this.colorLUTResolution = colorLUTResolution;
        this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
		this.shadowSettings = shadowSettings;
		this.useLightsPerObject = useLightsPerObject;
		this.postFXSettings = postFXSettings;
		this.allowHDR = allowHDR;
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
		GraphicsSettings.lightsUseLinearIntensity = true; //灯光使用线性强度
		InitializeForEditor();
		renderer = new CameraRenderer(cameraRendererShader);
	}

	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
		;
	}

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras) {
		for (int i = 0; i < cameras.Count; i++) {
			renderer.Render(context, cameras[i],allowHDR, useDynamicBatching, 
				useGPUInstancing, useLightsPerObject, shadowSettings, 
				postFXSettings, colorLUTResolution);
		}
	}

}