using UnityEditor;
using UnityEngine;

partial class PostFXStack {

	partial void ApplySceneViewState();//检查我们是否正在处理场景视图相机

#if UNITY_EDITOR

	partial void ApplySceneViewState() {//切换Post Processing选项
		if (
			camera.cameraType == CameraType.SceneView &&
			!SceneView.currentDrawingSceneView.sceneViewState.showImageEffects
		) {
			settings = null;
		}
	}

#endif
}
