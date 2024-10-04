using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor {
	public override void OnInspectorGUI() { 
		base.OnInspectorGUI();
		DrawRenderingLayerMask();
		if (!settings.lightType.hasMultipleDifferentValues && (LightType)settings.lightType.enumValueIndex == LightType.Spot) {
			settings.DrawInnerAndOuterSpotAngle();
			
		}
		var light = target as Light;
		if (light.cullingMask != -1) {
			EditorGUILayout.HelpBox(
				light.type == LightType.Directional ?
					"Culling Mask only affects shadows." :
					"Culling Mask only affects shadow unless Use Lights Per Objects is on.",
				MessageType.Warning
			);
		}
		settings.ApplyModifiedProperties();
	}
	static GUIContent renderingLayerMaskLabel = new GUIContent("Rendering Layer Mask", "Functional version of above property.");
	void DrawRenderingLayerMask() {
		SerializedProperty property = settings.renderingLayerMask;
		EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
		EditorGUI.BeginChangeCheck();
		int mask = property.intValue;
		if (mask == int.MaxValue) { //如果所有位置为1
			mask = -1;
		}
		mask = EditorGUILayout.MaskField(
			renderingLayerMaskLabel, mask,
			GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
		);
		if (EditorGUI.EndChangeCheck()) {
			property.intValue = mask == -1 ? int.MaxValue : mask;
		}
		EditorGUI.showMixedValue = false;
	}
}