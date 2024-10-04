/*
 * 设置光照基本属性，传递数据；
*/
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Net.Http.Headers;
public class Lighting {
	const string bufferName = "Lighting";
	CommandBuffer buffer = new CommandBuffer() { name = bufferName };
	//最大光源数量
	const int maxDirLightCount = 4;
	const int maxOtherLightCount = 64;
	static int
		//数量,颜色,(方向),阴影数据
		dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
		dirLightDirectionsAndMasksId = Shader.PropertyToID("_DirectionalLightDirectionsAndMasks"),
		dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

	static Vector4[]
		dirLightColors = new Vector4[maxDirLightCount],
		dirLightDirectionsAndMasks = new Vector4[maxDirLightCount],
		dirLightShadowData = new Vector4[maxDirLightCount];

	static int
		otherLightCountId = Shader.PropertyToID("_OtherLightCount"),//其他光源数量(点光源和面光源)
		otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
		otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
		otherLightDirectionsAndMasksId = Shader.PropertyToID("_OtherLightDirectionsAndMasks"),//聚光灯需要方向
		otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"), // 聚光灯内角
		otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");
	static Vector4[]
		otherLightColors = new Vector4[maxOtherLightCount],
		otherLightPositions = new Vector4[maxOtherLightCount],
		otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount],
		otherLightSpotAngles = new Vector4[maxOtherLightCount],
		otherLightShadowData = new Vector4[maxOtherLightCount];
	static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";
	CullingResults cullingResults;
	Shadows shadows = new Shadows();
	public void Setup(ScriptableRenderContext context, CullingResults cullingResults, 
		ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask) {
		this.cullingResults = cullingResults;
		buffer.BeginSample(bufferName);
		shadows.Setup(context, cullingResults, shadowSettings);
		SetupLights(useLightsPerObject, renderingLayerMask);
		shadows.Render();
		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	void SetupLights(bool useLightsPerObject, int renderingLayerMask) {
		//光源索引列表
		NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
		//得到所有可见光
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		int dirLightCount = 0, otherLightCount = 0;
		int i;
		for (i = 0; i < visibleLights.Length; i++) {
			int newIndex = -1;//只记录点光源和聚光灯的索引
			VisibleLight visibleLight = visibleLights[i];
			Light light = visibleLight.light;
			if ((light.renderingLayerMask & renderingLayerMask) != 0)
			{
				//根据光源类型设置光源数据
				switch (visibleLight.lightType)
				{
					case LightType.Directional:
						if (dirLightCount < maxDirLightCount)
						{
							SetupDirectionalLight(dirLightCount++, i, ref visibleLight, light);
						}
						break;
					case LightType.Point:
						if (otherLightCount < maxOtherLightCount)
						{//超过最大数量的光源不处理
							newIndex = otherLightCount;
							SetupPointLight(otherLightCount++, i, ref visibleLight, light);
						}
						break;
					case LightType.Spot:
						if (otherLightCount < maxOtherLightCount)
						{
							newIndex = otherLightCount;
							SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
						}
						break;
				}
			}
            //把visiblelight中的第i个光源的索引映射到 newIndex上。映射作用于unity变量unity_LightIndices。比如原来unity_LightIndices[0][0] =i，应用映射后为newIndex
            if (useLightsPerObject)
            {
                indexMap[i] = newIndex;
			}
			//将数据发送到GPU
			buffer.SetGlobalInt(dirLightCountId, dirLightCount);
			if (dirLightCount > 0) {
				buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
				buffer.SetGlobalVectorArray(dirLightDirectionsAndMasksId, dirLightDirectionsAndMasks);
				buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
			}
			buffer.SetGlobalInt(otherLightCountId, otherLightCount);
			if (otherLightCount > 0) {
				buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
				buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
				buffer.SetGlobalVectorArray(otherLightDirectionsAndMasksId, otherLightDirectionsAndMasks);
				buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
				buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
			}

		}
		if (useLightsPerObject) {
			for (; i < indexMap.Length; i++) {
				indexMap[i] = -1;
			}
			cullingResults.SetLightIndexMap(indexMap);
			Shader.EnableKeyword(lightsPerObjectKeyword);
			indexMap.Dispose();
		} else {
			Shader.DisableKeyword(lightsPerObjectKeyword);
		}
	}
		//
		//设置点光源数据,颜色和位置,
		//还有shadowData
		void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight,Light light) {
			dirLightColors[index] = visibleLight.finalColor;
			Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
			dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
			dirLightDirectionsAndMasks[index] = dirAndMask;
			dirLightShadowData[index] = shadows.ReserveDirectionalShadows(light, visibleIndex);

			//buffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
			//buffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);

		}
		//在cs中设置点光源数据,颜色和位置
		void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight,Light light) {
			otherLightColors[index] = visibleLight.finalColor;//光源颜色乘以强度,转换到了正确的色彩空间(线性)中
			Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
			position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
			otherLightPositions[index] = position;
			//不需要设置Direction,在计算中使用spotangels 取代
			otherLightSpotAngles[index] = new Vector4(0f, 1f);

			Vector4 dirAndmask = Vector4.zero;
			dirAndmask.w = light.renderingLayerMask;
			otherLightDirectionsAndMasks[index] = dirAndmask;
		//Light light = visibleLight.light;
			otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
		}
		//设置聚光灯数据,包括方向
		void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light) {
			otherLightColors[index] = visibleLight.finalColor;
			Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
			position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
			otherLightPositions[index] = position;
			Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
			dirAndMask.w = light.renderingLayerMask;
			otherLightDirectionsAndMasks[index] = dirAndMask;
			//内角外角衰减计算

			float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);//VisibleLight 中可能没有innerSpotAngle这个属性,所以用Light
			float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
			float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
			otherLightSpotAngles[index] = new Vector4(
				angleRangeInv, -outerCos * angleRangeInv
			);
			otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
		}
		public void Cleanup() {
			shadows.Cleanup();
		}
	}