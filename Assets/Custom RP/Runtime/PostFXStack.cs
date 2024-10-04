using UnityEngine;
using UnityEngine.Rendering;

using static PostFXSettings;
// 它使类或结构的所有常量、静态和类型成员都可以直接访问
public partial class PostFXStack {

	const string bufferName = "Post FX";

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};

	ScriptableRenderContext context;

	Camera camera;

	PostFXSettings settings;

	bool useHDR;
	public bool IsActive => settings != null;

	const int maxBloomPyramidLevels = 16;
	enum Pass {
		BloomAdd,
		BloomHorizontal,
		BloomPrefilter,
		BloomPrefilterFireflies,
		BloomScatter,
		BloomScatterFinal,
		BloomVertical,
		Copy,
        ColorGradingNone,
        ToneMappingACES,
		ToneMappingNeutral,
		ToneMappingReinhard,
		Final
	};
	int
		bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
		bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
		bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),//半分辨率
		fxSourceId = Shader.PropertyToID("_PostFXSource"),//中间帧缓冲区的数据
		bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
		bloomResultId = Shader.PropertyToID("_BloomResult"), //存储bloom结果,随后进行色调分级
		fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
		colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
		colorFilterId = Shader.PropertyToID("_ColorFilter"),
		whiteBalanceId = Shader.PropertyToID("_WhiteBalance"),
		splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows"),
		splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights"),
		channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
		channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
		channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
		smhShadowsId = Shader.PropertyToID("_SMHShadows"),
		smhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
		smhHighlightsId = Shader.PropertyToID("_SMHHighlights"),
		smhRangeId = Shader.PropertyToID("_SMHRange"),
		colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT"),
		colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters"),
		colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLog");



	int bloomPyramidId;//第一个纹理的Id
    int colorLUTResolution;

    public PostFXStack() {
		//一次获取所有标识符Id. 只需保存第一个
		//unity按照请求新属性名的顺序顺序分配标识符,一次获取的Id是连续的
		bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
		for (int i = 1; i < maxBloomPyramidLevels * 2; i++) {
			Shader.PropertyToID("_BloomPyramid" + i);
		}
	}
	//为给定的源标识符应用Bloom
	bool DoBloom(int sourceId) {

		PostFXSettings.BloomSettings bloom = settings.Bloom;
		int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		//如果迭代次数为0或者高度或宽度小于downscaleLimit,则直接拷贝
		//乘2是因为要进行半分辨率处理,...
		if (bloom.maxIterations == 0 || bloom.intensity <= 0f || height < bloom.downscaleLimit * 2
			|| width < bloom.downscaleLimit * 2) {
			return false;
		}
		buffer.BeginSample("Bloom");
		Vector4 threshold;
		threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
		threshold.y = threshold.x * bloom.thresholdKnee;
		threshold.z = 2f * threshold.y;
		threshold.w = 0.25f / (threshold.y + 0.00001f);
		threshold.y -= threshold.x;
		buffer.SetGlobalVector(bloomThresholdId, threshold);

		RenderTextureFormat format = useHDR ?
			RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

		//半分辨率,即先将原图像的尺寸减半,将该图像作为原始纹理
		buffer.GetTemporaryRT(
			bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format
		);
		Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ?
				Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
		width /= 2;
		height /= 2;


		int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;

		//遍历渲染各个层级
		int i = 0;
		for (i = 0; i < bloom.maxIterations; i++) {
			if (height < bloom.downscaleLimit || width < bloom.downscaleLimit) {
				break;
			}
			int midId = toId - 1;
			//中间与目标
			// 过滤模式（Filter Mode）FilterMode.Bilinear.过滤模式决定了在纹理被放大或缩小时，如何处理像素。
			buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
			buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
			Draw(fromId, midId, Pass.BloomHorizontal);
			Draw(midId, toId, Pass.BloomVertical);
			fromId = toId;
			toId += 2;
			width /= 2;
			height /= 2;
		}
		buffer.ReleaseTemporaryRT(bloomPrefilterId);
		//是否进行三重
		buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
		Pass combinePass, finalPass;
		float finalIntensity;
		if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive) {
			combinePass = finalPass = Pass.BloomAdd;
			buffer.SetGlobalFloat(bloomIntensityId, 1f);
			finalIntensity = bloom.intensity;
		}
		else {
			combinePass = Pass.BloomScatter;
			finalPass = Pass.BloomScatterFinal;
			buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
			finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
		}
		//只有当迭代次数大于1时，才会上采样
		if (i > 1) {
			buffer.ReleaseTemporaryRT(fromId - 1);
			toId -= 5;
			//Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			for (i -= 1; i > 0; i--) {
				buffer.SetGlobalTexture(fxSource2Id, toId + 1);
				Draw(fromId, toId, combinePass);
				buffer.ReleaseTemporaryRT(fromId);
				buffer.ReleaseTemporaryRT(toId + 1);
				fromId = toId;
				toId -= 2;
			}
		}
		else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
		buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		buffer.GetTemporaryRT(
		bloomResultId, camera.pixelWidth, camera.pixelHeight, 0,
			FilterMode.Bilinear, format
		);
		Draw(fromId, bloomResultId, finalPass);
		buffer.ReleaseTemporaryRT(fromId);
		buffer.EndSample("Bloom");
		return true;
	}

	void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass) {
		buffer.SetGlobalTexture(fxSourceId, from);//设置全局纹理,即是把中间帧缓冲区的数据发送到GPU上
		buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
	}
	//static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);
	void DrawFinal(RenderTargetIdentifier from) {
		buffer.SetGlobalTexture(fxSourceId, from);
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			RenderBufferLoadAction.Load,
			RenderBufferStoreAction.Store
		);
		buffer.SetViewport(camera.pixelRect);
		buffer.DrawProcedural(Matrix4x4.identity, settings.Material, 
			(int)Pass.Final, MeshTopology.Triangles, 3
		);
	}
	public void Setup(
		ScriptableRenderContext context, Camera camera, PostFXSettings settings,
		bool useHDR, int colorLUTResolution
	) {
        this.colorLUTResolution = colorLUTResolution;
        this.context = context;
		this.camera = camera;
		this.useHDR = useHDR;
		//检查是否相机渲染Game或Scene视图.如果没有，则将后处理特效资产配置设为空，使得该相机停止渲染后处理特效。
		this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
		ApplySceneViewState();
	}
    //传递颜色分级参数
    void ConfigureColorAdjustments() {
		ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
		buffer.SetGlobalVector(colorAdjustmentsId, new Vector4(
			Mathf.Pow(2f, colorAdjustments.postExposure),
			colorAdjustments.contrast * 0.01f + 1f, //-1-1
			colorAdjustments.hueShift * (1f / 360f),//0-2
			colorAdjustments.saturation * 0.01f + 1f
		));
		buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear);
	}
    //配置白平衡
    void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
        buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(
            whiteBalance.temperature, whiteBalance.tint
        ));
    }
	//色调分离
    void ConfigureSplitToning()
    {
        SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowsId, splitColor);
        buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
    }
    void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
    }

    void ConfigureShadowsMidtonesHighlights()
    {
        ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
        buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
        buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
        buffer.SetGlobalVector(smhRangeId, new Vector4(
            smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd
        ));
    }

    void DoColorGradingAndToneMapping(int sourceId) {//颜色分级与色调映射
		ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHighlights();
        
		//LUT
		int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        buffer.GetTemporaryRT(
            colorGradingLUTId, lutWidth, lutHeight, 0,
            FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
        );
		buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
			lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)));
		ToneMappingSettings.Mode mode = settings.ToneMapping.mode;

        Pass pass = Pass.ColorGradingNone + (int)mode;//绘制到LUT而不是相机目标
		buffer.SetGlobalFloat(
			colorGradingLUTInLogId, useHDR && pass != Pass.ColorGradingNone ? 1f : 0f
		);
		Draw(sourceId, colorGradingLUTId, pass);
		
		buffer.SetGlobalVector(colorGradingLUTParametersId, //不是很明白这些参数作用
			new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f)
		);
		DrawFinal(sourceId);
        buffer.ReleaseTemporaryRT(colorGradingLUTId);
    }
	public void Render(int sourceId) {
		//只需使用适当的着色器绘制一个覆盖整个图像的矩形，即可将效果应用于整个图像。
		//Blit 将sourceId的内容复制到BuiltinRenderTextureType.CameraTarget,
		//后者代表了当前渲染摄像机的目标纹理
		//buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
		if (DoBloom(sourceId)) {
			DoColorGradingAndToneMapping(bloomResultId);
			buffer.ReleaseTemporaryRT(bloomResultId);
		}
		else {
			DoColorGradingAndToneMapping(sourceId);
		}
		//DoBloom(sourceId);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
}