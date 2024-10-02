using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack {

	const string bufferName = "Post FX";

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};

	ScriptableRenderContext context;

	Camera camera;

	PostFXSettings settings;

	public bool IsActive => settings != null;

	const int maxBloomPyramidLevels = 16;
	enum Pass {
		BloomCombine,
		BloomHorizontal,
		BloomVertical,
		Copy
	};
	int
		fxSourceId = Shader.PropertyToID("_PostFXSource"),//中间帧缓冲区的数据
		fxSource2Id = Shader.PropertyToID("_PostFXSource2");

	int bloomPyramidId;//第一个纹理的Id
	public PostFXStack() {
		//一次获取所有标识符Id. 只需保存第一个
		//unity按照请求新属性名的顺序顺序分配标识符,一次获取的Id是连续的
		bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
		for (int i = 1; i < maxBloomPyramidLevels*2; i++) {
			Shader.PropertyToID("_BloomPyramid" + i);
		}
	}
	//为给定的源标识符应用Bloom
	void DoBloom(int sourceId) {
		buffer.BeginSample("Bloom");
		PostFXSettings.BloomSettings bloom = settings.Bloom;
		int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		//如果迭代次数为0或者高度或宽度小于downscaleLimit,则直接拷贝
		if (bloom.maxIterations == 0 || height < bloom.downscaleLimit 
			|| width < bloom.downscaleLimit) {
			Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			buffer.EndSample("Bloom");
			return;
		}
		RenderTextureFormat format = RenderTextureFormat.Default;
		int fromId = sourceId, toId = bloomPyramidId + 1;

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
		//只有当迭代次数大于1时，才会上采样
		if (i > 1) {
			buffer.ReleaseTemporaryRT(fromId - 1);
			toId -= 5;
			//Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			for (i -= 1; i > 0; i--) {
				buffer.SetGlobalTexture(fxSource2Id, toId + 1);
				Draw(fromId, toId, Pass.BloomCombine);
				buffer.ReleaseTemporaryRT(fromId);
				buffer.ReleaseTemporaryRT(toId + 1);
				fromId = toId;
				toId -= 2;
			}
		} else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);
		buffer.ReleaseTemporaryRT(fromId);
		buffer.EndSample("Bloom");
	}

	void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass) {
		buffer.SetGlobalTexture(fxSourceId, from);//设置全局纹理,即是把中间帧缓冲区的数据发送到GPU上
		buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);   
	}
	public void Setup(
		ScriptableRenderContext context, Camera camera, PostFXSettings settings
	) {
		this.context = context;
		this.camera = camera;
		//检查是否相机渲染Game或Scene视图.如果没有，则将后处理特效资产配置设为空，使得该相机停止渲染后处理特效。
		this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
		ApplySceneViewState();
	}

	public void Render(int sourceId) {
		//只需使用适当的着色器绘制一个覆盖整个图像的矩形，即可将效果应用于整个图像。
		//Blit 将sourceId的内容复制到BuiltinRenderTextureType.CameraTarget,
		//后者代表了当前渲染摄像机的目标纹理
		//buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
		DoBloom(sourceId);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
}