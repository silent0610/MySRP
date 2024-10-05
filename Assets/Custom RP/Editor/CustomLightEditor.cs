using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor {
	public override void OnInspectorGUI() { 
		base.OnInspectorGUI();
		//这里的settings是LightEditor的私有变量,存储了Light的
		RenderingLayerMaskDrawer.Draw(
            settings.renderingLayerMask, renderingLayerMaskLabel
        );
        if (!settings.lightType.hasMultipleDifferentValues && 
			(LightType)settings.lightType.enumValueIndex == LightType.Spot) {
			settings.DrawInnerAndOuterSpotAngle();
		}
		settings.ApplyModifiedProperties();
		var light = target as Light;
		if (light.cullingMask != -1) {
			EditorGUILayout.HelpBox(
				light.type == LightType.Directional ?
					"Culling Mask only affects shadows." :
					"Culling Mask only affects shadow unless Use Lights Per Objects is on.",
				MessageType.Warning
			);
		}
		
	}
	static GUIContent renderingLayerMaskLabel = 
		new GUIContent("Rendering Layer Mask", "Functional version of above property.");

}