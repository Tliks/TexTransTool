#nullable enable

using UnityEditor;
using net.rs64.TexTransTool.MultiLayerImage;

namespace net.rs64.TexTransTool.Editor.MultiLayerImage
{
    [CustomEditor(typeof(HSVSimpleAdjustmentLayer), true)]
    [CanEditMultipleObjects]
    internal class HSVSimpleAdjustmentLayerEditor : AbstractLayerEditor
    {
        protected override void DrawInnerProperties()
        {
            var hue = serializedObject.FindProperty(nameof(HSVSimpleAdjustmentLayer.Hue));
            EditorGUILayout.PropertyField(hue, "HSVSimpleAdjustmentLayer:prop:Hue".Glc());

            var saturation = serializedObject.FindProperty(nameof(HSVSimpleAdjustmentLayer.Saturation));
            EditorGUILayout.PropertyField(saturation, "HSVSimpleAdjustmentLayer:prop:Saturation".Glc());

            var value = serializedObject.FindProperty(nameof(HSVSimpleAdjustmentLayer.Value));
            EditorGUILayout.PropertyField(value, "HSVSimpleAdjustmentLayer:prop:Value".Glc());
        }
    }
}

