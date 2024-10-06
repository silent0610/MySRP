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
	//static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
	//分离帧缓冲区
	static int
		colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"), 
		depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
		colorTextureId = Shader.PropertyToID("_CameraColorTexture"),
		depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
		sourceTextureId = Shader.PropertyToID("_SourceTexture"), //相机shader使用的纹理
		srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
		dstBlendId = Shader.PropertyToID("_CameraDstBlend");

	static CameraSettings defaultCameraSettings = new CameraSettings();//默认相机设置

	bool useColorTexture, useDepthTexture,useIntermediateBuffer;

	bool useHDR;

	Material material;

	Texture2D missingTexture; //不存在的深度纹理

	static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

	public CameraRenderer(Shader shader) {
		material = CoreUtils.CreateEngineMaterial(shader);
		missingTexture = new Texture2D(1, 1) {
			hideFlags = HideFlags.HideAndDontSave,
			name = "Missing"
		};
		missingTexture.SetPixel(0, 0, Color.white * 0.5f);
		missingTexture.Apply(true, true);
	}

	public void Dispose() {
		CoreUtils.Destroy(material);
		CoreUtils.Destroy(missingTexture);
	}

	/// <summary>
	/// 复制深度缓冲区
	/// </summary>
	void CopyAttachments() {
		if (useColorTexture) {
			buffer.GetTemporaryRT(
				colorTextureId, camera.pixelWidth, camera.pixelHeight,
				0, FilterMode.Bilinear, useHDR ?
					RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
			);
			if (copyTextureSupported) {
				buffer.CopyTexture(colorAttachmentId, colorTextureId);
			}
			else {
				Draw(colorAttachmentId, colorTextureId);
			}
		}
		if (useDepthTexture) {
			buffer.GetTemporaryRT(
				depthTextureId, camera.pixelWidth, camera.pixelHeight,
				32, FilterMode.Point, RenderTextureFormat.Depth
			);
			if (copyTextureSupported) {
				buffer.CopyTexture(depthAttachmentId, depthTextureId);
			}
			else {
				Draw(depthAttachmentId, depthTextureId,true);
			}
			
		}
		if (!copyTextureSupported) {
			buffer.SetRenderTarget( //Draw函数修改了当前渲染目标，所以需要重新设置
				colorAttachmentId,
				RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
				depthAttachmentId,
				RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
			);
		}
		ExecuteBuffer();
	}
	void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to,bool isDepth = false) {
		buffer.SetGlobalTexture(sourceTextureId, from);
		buffer.SetRenderTarget(
			to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		buffer.DrawProcedural(
			Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3
		);
		//material 对应的shader的 pass0
	}

	public void Render(ScriptableRenderContext context, Camera camera,CameraBufferSettings bufferSettings, 
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, 
		ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution) {
		this.context = context;
		this.camera = camera;

		var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
		CameraSettings cameraSettings =
			crpCamera ? crpCamera.Settings : defaultCameraSettings;
		
		//控制是否启用深度纹理
		if (camera.cameraType == CameraType.Reflection) {
			useDepthTexture = bufferSettings.copyDepthReflection;
			useColorTexture = bufferSettings.copyColorReflection;
		}
		else {
			useDepthTexture = bufferSettings.copyDepth && cameraSettings.copyDepth;//管线和相机都启用
			useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
		}

		if (cameraSettings.overridePostFX) { 
			postFXSettings = cameraSettings.postFXSettings;
		}
		PrepareBuffer();
		PrepareForSceneWindow();
		if (!Cull(shadowSettings.maxDistance)) {
			return;
		}
		useHDR = bufferSettings.allowHDR && camera.allowHDR; //管线允许HDR且相机允许HDR
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
		lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject,
            cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
		postFXStack.Setup(context, camera, postFXSettings, useHDR, 
			colorLUTResolution, cameraSettings.finalBlendMode);
		buffer.EndSample(SampleName);
		Setup();
		DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject,
			cameraSettings.renderingLayerMask
		);
		DrawUnsupportedShaders();
		DrawGizmosBeforeFX();
		if (postFXStack.IsActive) {//fx
			postFXStack.Render(colorAttachmentId);
		}
		else if (useIntermediateBuffer) {
			DrawFinal(cameraSettings.finalBlendMode);
			ExecuteBuffer();
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
		useIntermediateBuffer = useColorTexture||useDepthTexture || postFXStack.IsActive;
		//获取中间帧缓冲区
		if (useIntermediateBuffer) {
			//为了防止随机结果，当堆栈处于活动状态时，始终清除深度和颜色。
			if (flags > CameraClearFlags.Color) {
				flags = CameraClearFlags.Color;
			}
			
			buffer.GetTemporaryRT(
				colorAttachmentId, camera.pixelWidth, camera.pixelHeight,
				0, FilterMode.Bilinear, useHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default
			);
			buffer.GetTemporaryRT(
				depthAttachmentId, camera.pixelWidth, camera.pixelHeight,
				32, FilterMode.Point, RenderTextureFormat.Depth
			);
			buffer.SetRenderTarget(//将中间帧缓冲区设置为当前渲染目标
				colorAttachmentId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
				depthAttachmentId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
			);
		}


		buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags <= CameraClearFlags.Color, flags == CameraClearFlags.Color ?
				camera.backgroundColor.linear : Color.clear);
		buffer.BeginSample(SampleName);
		buffer.SetGlobalTexture(colorTextureId, missingTexture);
		buffer.SetGlobalTexture(depthTextureId, missingTexture);
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
	void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, 
		bool useLightsPerObject, int renderingLayerMask) {
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
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque,
            renderingLayerMask: (uint)renderingLayerMask
        );
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
		context.DrawSkybox(camera);

		CopyAttachments();

		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawingSettings.sortingSettings = sortingSettings;
		filteringSettings.renderQueueRange = RenderQueueRange.transparent;

		context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
	}
	void Cleanup() {
		lighting.Cleanup();
		if (useIntermediateBuffer) {
			buffer.ReleaseTemporaryRT(colorAttachmentId);
			buffer.ReleaseTemporaryRT(depthAttachmentId);

			if (useColorTexture || useDepthTexture) {
				CopyAttachments();
			}
		}
	}
	static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);
	void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode) {
		buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
		buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
		buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ?
				RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
			RenderBufferStoreAction.Store
		);
		buffer.SetViewport(camera.pixelRect);
		buffer.DrawProcedural(
			Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3
		);
		buffer.SetGlobalFloat(srcBlendId, 1f);
		buffer.SetGlobalFloat(dstBlendId, 0f);
	}
}