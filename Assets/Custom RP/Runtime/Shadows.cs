using UnityEngine;
using UnityEngine.Rendering;

public class Shadows {

	const string bufferName = "Shadows";

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};

	ScriptableRenderContext context;

	CullingResults cullingResults;

	ShadowSettings settings;

	const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

	struct ShadowedDirectionalLight {
		public int visibleLightIndex;
		public float slopeScaleBias;
		public float nearPlaneOffset;
	}
	ShadowedDirectionalLight[] ShadowedDirectionalLights =
	new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
	
	//已存储的可投射阴影的平行光数量
	int ShadowedDirectionalLightCount;

	static int
		dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
		dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
		cascadeCountId = Shader.PropertyToID("_CascadeCount"),
		cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
		cascadeDataId = Shader.PropertyToID("_CascadeData"),
		shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

	//存储阴影转换矩阵,世界空间的一点到级联贴图中的一点的转换矩阵
	static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
	static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades], cascadeData = new Vector4[maxCascades];

	static string[] directionalFilterKeywords =
{
		"_DIRECTIONAL_PCF3",
		"_DIRECTIONAL_PCF5",
		"_DIRECTIONAL_PCF7",
	};

	static string[] cascadeBlendKeywords = { "_CASCADE_BLEND_SOFT", "_CASCADE_BLEND_DITHER" };

	//关键字数组，控制是否使用阴影蒙版
	static string[] shadowMaskKeywords = { "_SHADOW_MASK_ALWAYS", "_SHADOW_MASK_DISTANCE" };

	bool useShadowMask; //根据变量值，确定是否使用阴影蒙版，是否启用关键字
						// 设置着色器关键字
	void SetKeywords(string[] keywords, int enabledIndex) {
		//int enabledIndex = (int)settings.directional.filter - 1;
		for (int i = 0; i < keywords.Length; i++) {
			if (i == enabledIndex) {
				//需要事先使用#pragma 声明关键字
				buffer.EnableShaderKeyword(keywords[i]);
			} else {
				buffer.DisableShaderKeyword(keywords[i]);
			}
		}
	}
	//存储可见光的阴影数据
	public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex) {
		//存储可见光源的索引，前提是光源开启了阴影投射并且阴影强度不能为0 
		if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount && light.shadows != LightShadows.None && light.shadowStrength > 0f) {
			float maskChannel = -1;
			//点光源或聚光灯如果使用了ShadowMask
			LightBakingOutput lightBaking = light.bakingOutput;
			if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask) {
				useShadowMask = true;
				maskChannel = lightBaking.occlusionMaskChannel;
			}
			//是否在阴影最大投射距离内，有被该光源影响且需要投影的物体存在，如果没有就不需要渲染该光源的阴影贴图了
			if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) {
				return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
			}
			ShadowedDirectionalLights[ShadowedDirectionalLightCount] =
				new ShadowedDirectionalLight {
					visibleLightIndex = visibleLightIndex,
					slopeScaleBias = light.shadowBias,
					nearPlaneOffset = light.shadowNearPlane
				};
			//shadowStrength ,该光源的阴影强度
			return new Vector4(light.shadowStrength, settings.directional.cascadeCount * ShadowedDirectionalLightCount++, light.shadowNormalBias, maskChannel);
		}
		return new Vector4(0f, 0f, 0f, -1f);
	}
	//存储其他可见光的阴影数据
	public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex) {
		if (light.shadows != LightShadows.None && light.shadowStrength > 0f) {
			LightBakingOutput lightBaking = light.bakingOutput;
			if (
				lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
				lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
			) {
				useShadowMask = true;
				return new Vector4(
					light.shadowStrength, 0f, 0f,
					lightBaking.occlusionMaskChannel
				);
			}
		}
		return new Vector4(0f, 0f, 0f, -1f);
	}
	public void Setup(
		ScriptableRenderContext context, CullingResults cullingResults,
		ShadowSettings settings
	) {
		this.context = context;
		this.cullingResults = cullingResults;
		this.settings = settings;
		ShadowedDirectionalLightCount = 0;
		useShadowMask = false;
	}

	void ExecuteBuffer() {
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	public void Render() {
		if (ShadowedDirectionalLightCount > 0) {
			RenderDirectionalShadows();
		} else {
			buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		}
		buffer.BeginSample(bufferName);
		//是否使用阴影蒙版
		SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
	static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
	
	//渲染方向光阴影
	void RenderDirectionalShadows() {
		int atlasSize = (int)settings.directional.atlasSize;
		//创建renderTexture，并指定该类型是阴影贴图
		buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.BeginSample(bufferName);
		buffer.ClearRenderTarget(true, false, Color.clear);
		ExecuteBuffer();
		int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
		int tileSize = atlasSize / split;
		for (int i = 0; i < ShadowedDirectionalLightCount; i++) {
			RenderDirectionalShadow(i, split, tileSize);
		}
		buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
		buffer.SetGlobalVectorArray(
			cascadeCullingSpheresId, cascadeCullingSpheres
		);
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
		float f = 1f - settings.directional.cascadeFade;
		buffer.SetGlobalVector(
			shadowDistanceFadeId, new Vector4(
				1f / settings.maxDistance, 1f / settings.distanceFade,
				1f / (1f - f * f)
			)
		);
		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
		SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
		SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
		buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));

		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
	void SetCascadeData(int index, Vector4 cullingSphere, float tileSize) {
		float texelSize = 2f * cullingSphere.w / tileSize;
		float filterSize = texelSize * ((float)settings.directional.filter + 1f);
		cullingSphere.w -= filterSize;
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
		cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
	}
	void RenderDirectionalShadow(int index, int split, int tileSize) {
		ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic);

		int cascadeCount = settings.directional.cascadeCount;
		int tileOffset = index * cascadeCount;
		Vector3 ratios = settings.directional.CascadeRatios;
		float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
		for (int i = 0; i < cascadeCount; i++) {
			cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);


			splitData.shadowCascadeBlendCullingFactor = cullingFactor;

			shadowSettings.splitData = splitData;
			if (index == 0) {
				SetCascadeData(i, splitData.cullingSphere, tileSize);
			}
			int tileIndex = tileOffset + i;
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix,
				SetTileViewport(tileIndex, split, tileSize), split
			);
			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
		}
	}
	//调整渲染视口来渲染单个图块
	Vector2 SetTileViewport(int index, int split, float tileSize) {

		Vector2 offset = new Vector2(index % split, index / split);
		buffer.SetViewport(new Rect(
			offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
		));
		return offset;
	}
	//返回一个从世界空间转到阴影纹理图块空间的矩阵
	Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split) {
		if (SystemInfo.usesReversedZBuffer) {
			m.m20 = -m.m20;
			m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
		}
		float scale = 1f / split;
		m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
		m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
		m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
		m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
		m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
		m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
		m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
		m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
		m.m20 = 0.5f * (m.m20 + m.m30);
		m.m21 = 0.5f * (m.m21 + m.m31);
		m.m22 = 0.5f * (m.m22 + m.m32);
		m.m23 = 0.5f * (m.m23 + m.m33);
		return m;
	}
	//释放临时渲染纹理
	public void Cleanup() {
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
		ExecuteBuffer();
	}

}