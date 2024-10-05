using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour {
	[SerializeField]
	CameraSettings settings = default;

	//若为空,创建新的,也就是说怎么都空不了呗
	public CameraSettings Settings => settings ?? (settings = new CameraSettings());
}