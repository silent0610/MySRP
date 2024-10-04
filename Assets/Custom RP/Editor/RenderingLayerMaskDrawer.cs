using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer
{
    //������ Unity �༭�����Զ������Խ�������ʾһ������ѡ�� "Rendering Layer Mask" �Ŀؼ���
    //Rect position: ָ�����ƿؼ��ľ�������
    //SerializedProperty property: ���л����ԣ�ͨ�������� Unity �༭���е����ݽ��а󶨡�
    //GUIContent label: �ؼ��ı�ǩ��������ʾ�ؼ������������ơ�
    public static void Draw(
        Rect position, SerializedProperty property, GUIContent label
    )
    {
        //SerializedProperty property = settings.renderingLayerMask;
        //��鵱ǰ�����Ƿ���в�ͬ��ֵ�����ڴ����ѡ���
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        bool isUint = property.type == "uint";
        if (isUint && mask == int.MaxValue)
        {
            mask = -1;
        }
        mask = EditorGUI.MaskField( // ʹ�� MaskField ����������һ��������ѡ��ؼ���ѡ�������ڵ�ǰ��Ⱦ���ߵ� 
            position, label, mask,
            GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
        );
        if (EditorGUI.EndChangeCheck())//�Ƿ����˱仯������б仯�����ջ���� property.intValue��
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