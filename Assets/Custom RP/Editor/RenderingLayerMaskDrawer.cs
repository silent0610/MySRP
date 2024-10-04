using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer
{
    //用于在 Unity 编辑器的自定义属性界面中显示一个用于选择 "Rendering Layer Mask" 的控件。
    //Rect position: 指定绘制控件的矩形区域。
    //SerializedProperty property: 序列化属性，通常用于与 Unity 编辑器中的数据进行绑定。
    //GUIContent label: 控件的标签，用于显示控件的描述或名称。
    public static void Draw(
        Rect position, SerializedProperty property, GUIContent label
    )
    {
        //SerializedProperty property = settings.renderingLayerMask;
        //检查当前属性是否具有不同的值（用于处理多选情况
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        bool isUint = property.type == "uint";
        if (isUint && mask == int.MaxValue)
        {
            mask = -1;
        }
        mask = EditorGUI.MaskField( // 使用 MaskField 方法来绘制一个层遮罩选择控件。选项来自于当前渲染管线的 
            position, label, mask,
            GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
        );
        if (EditorGUI.EndChangeCheck())//是否发生了变化，如果有变化，最终会更新 property.intValue。
        {
            property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
        }
        EditorGUI.showMixedValue = false;
    }
    public static void Draw(SerializedProperty property, GUIContent label)
    {
        Draw(EditorGUILayout.GetControlRect(), property, label);
    }
    public override void OnGUI(
    Rect position, SerializedProperty property, GUIContent label
)
    {
        Draw(position, property, label);
    }
}