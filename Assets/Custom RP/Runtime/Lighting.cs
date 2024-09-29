/*
 * 设置光照基本属性，传递数据；
*/
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Net.Http.Headers;
public class Lighting
{
    const string bufferName = "Lighting";
    CommandBuffer buffer = new CommandBuffer() { name = bufferName };
	//最大光源数量
    const int maxDirLightCount = 4;
	const int maxOtherLightCount = 64;
	static int
		//数量,颜色,(方向),阴影数据
		dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
		dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
		dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
	
    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
		dirLightShadowData = new Vector4[maxDirLightCount];

	static int
		otherLightCountId = Shader.PropertyToID("_OtherLightCount"),//其他光源数量(点光源和面光源)
		otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
		otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
		otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),//聚光灯需要方向
		otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"); // 聚光灯内角
	static Vector4[]
		otherLightColors = new Vector4[maxOtherLightCount],
		otherLightPositions = new Vector4[maxOtherLightCount],
		otherLightDirections = new Vector4[maxOtherLightCount],
		otherLightSpotAngles = new Vector4[maxOtherLightCount];

	CullingResults cullingResults;
	Shadows shadows = new Shadows();
	public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
		shadows.Setup(context, cullingResults, shadowSettings);
		SetupLights();
		shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0, otherLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
			//根据光源类型设置光源数据
			switch (visibleLight.lightType) {
				case LightType.Directional:
					if (dirLightCount < maxDirLightCount) {
						SetupDirectionalLight(dirLightCount++, ref visibleLight);
					}
					break;
				case LightType.Point:
					if (otherLightCount < maxOtherLightCount) {//超过最大数量的光源不处理
						SetupPointLight(otherLightCount++, ref visibleLight);
					}
					break;
				case LightType.Spot:
					if (otherLightCount < maxOtherLightCount) {
						SetupSpotLight(otherLightCount++, ref visibleLight);
					}
					break;
			}

        }
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
		if (dirLightCount > 0) {
			buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
			buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
			buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
		}
		buffer.SetGlobalInt(otherLightCountId, otherLightCount);
		if (otherLightCount > 0) {
			buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
			buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
			buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
			buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
		}
	
	}
	//设置点光源数据,颜色和位置,
	//还有shadowData
	void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
        
        //buffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
        //buffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);

    }
	//在cs中设置点光源数据,颜色和位置
	void SetupPointLight(int index, ref VisibleLight visibleLight) { 
		otherLightColors[index] = visibleLight.finalColor;//光源颜色乘以强度,转换到了正确的色彩空间(线性)中
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		//不需要设置Direction,在计算中使用spotangels 取代
		otherLightSpotAngles[index] = new Vector4(0f, 1f);
	}
	//设置聚光灯数据,包括方向
	void SetupSpotLight(int index, ref VisibleLight visibleLight) { 
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

		//内角外角衰减计算
		Light light = visibleLight.light;
		float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);//VisibleLight 中可能没有innerSpotAngle这个属性,所以用Light
		float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
		float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
		otherLightSpotAngles[index] = new Vector4(
			angleRangeInv, -outerCos * angleRangeInv
		);
	}
	public void Cleanup() {
		shadows.Cleanup();
	}
}