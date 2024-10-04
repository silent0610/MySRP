using System;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer {
	ScriptableRenderContext context;
	Camera camera;
	const string bufferName = "Render Camera";
	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};
	CullingResults cullingResults;
	// 要渲染的Pass 的Tag
	static ShaderTagId 
		unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
		litShaderTagId = new ShaderTagId("CustomLit");

	Lighting lighting = new Lighting();

	PostFXStack postFXStack = new PostFXStack();
	//摄像机的中间帧缓冲区
	static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

	static CameraSettings defaultCameraSettings = new CameraSettings();//默认相机设置

	bool useHDR;
	public void Render(ScriptableRenderContext context, Camera camera,bool allowHDR, 
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, 
		ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution) {
		this.context = context;
		this.camera = camera;

		var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
		CameraSettings cameraSettings =
			crpCamera ? crpCamera.Settings : defaultCameraSettings;

		if (cameraSettings.overridePostFX) { 
			postFXSettings = cameraSettings.postFXSettings;
		}
		PrepareBuffer();
		PrepareForSceneWindow();
		if (!Cull(shadowSettings.maxDistance)) {
			return;
		}
		useHDR = allowHDR && camera.allowHDR;//管线允许HDR且相机允许HDR
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
		lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject);
		postFXStack.Setup(context, camera, postFXSettings, useHDR, 
			colorLUTResolution, cameraSettings.finalBlendMode);
		buffer.EndSample(SampleName);
		Setup();
		DrawVisiableGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
		DrawUnsupportedShaders();
		DrawGizmosBeforeFX();
		if (postFXStack.IsActive) {//fx
			postFXStack.Render(frameBufferId);
		}
		DrawGizmosAfterFX();
		Cleanup();
		Submit();
	}

	bool Cull(float maxShadowDistance) {
		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
			p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
			cullingResults = context.Cull(ref p);
			return true;
		}
		return false;
	}
	void Setup() {
		context.SetupCameraProperties(camera);
		CameraClearFlags flags = camera.clearFlags;
		//获取中间帧缓冲区
		if (postFXStack.IsActive) {
			if (flags > CameraClearFlags.Color) {
				flags = CameraClearFlags.Color;
			}
			//为了防止随机结果，当堆栈处于活动状态时，始终清除深度和颜色。
			buffer.GetTemporaryRT(
				frameBufferId, camera.pixelWidth, camera.pixelHeight,
				32, FilterMode.Bilinear, useHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default
			);
			buffer.SetRenderTarget(//将中间帧缓冲区设置为当前渲染目标
				frameBufferId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
			);
		}


		buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags <= CameraClearFlags.Color, flags == CameraClearFlags.Color ?
				camera.backgroundColor.linear : Color.clear);
		buffer.BeginSample(SampleName);
		ExecuteBuffer();

	}
	void Submit() {
		buffer.EndSample(SampleName);
		ExecuteBuffer();
		context.Submit();
	}
	void ExecuteBuffer() {
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	//画出可见物体
	void DrawVisiableGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject) {
		PerObjectData lightsPerObjectFlags = useLightsPerObject ?
		PerObjectData.LightData | PerObjectData.LightIndices :
		PerObjectData.None;

		var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
		//设置渲染的基础Pass
		var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings) {
			enableDynamicBatching = useDynamicBatching,
			enableInstancing = useGPUInstancing,
			//发送到GPU的数据
			perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask | PerObjectData.LightProbe
				| PerObjectData.OcclusionProbe | PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume
				| PerObjectData.ReflectionProbes | lightsPerObjectFlags

		};
		//设置要渲染的Pass 即CusotmLit
		drawingSettings.SetShaderPassName(1, litShaderTagId);
		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
		context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
		context.DrawSkybox(camera);

		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawingSettings.sortingSettings = sortingSettings;
		filteringSettings.renderQueueRange = RenderQueueRange.transparent;

		context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
	}
	void Cleanup() {
		lighting.Cleanup();
		if (postFXStack.IsActive) {
			buffer.ReleaseTemporaryRT(frameBufferId);
		}
	}

}