using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonoBehaviour), true)]
public class FusionVRPlusScriptStatusDrawer : Editor
{
    private bool _checked;
    private bool _isDeprecated;
    private bool _isWip;

    private const string DeprecatedFolderPath = "Assets/Scripts/FusionVR+/Deprecated Scripts/";
    private const string WipFolderPath = "Assets/Scripts/FusionVR+/Wip/";

    public override void OnInspectorGUI()
    {
        CheckScriptStatus();

        if (_isDeprecated)
        {
            EditorGUILayout.HelpBox(
                "This script is currently DEPRECATED. Consider removing or replacing it.",
                MessageType.Warning
            );
        }

        if (_isWip)
        {
            EditorGUILayout.HelpBox(
                "This script is WORK IN PROGRESS. Expect bugs or incomplete features.",
                MessageType.Info
            );
        }

        base.OnInspectorGUI();
    }

    private void CheckScriptStatus()
    {
        if (_checked)
            return;

        if (target is not MonoBehaviour targetBehaviour)
            return;

        var script = MonoScript.FromMonoBehaviour(targetBehaviour);
        if (script == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(script);

        _isDeprecated = assetPath.Contains(DeprecatedFolderPath);
        _isWip = assetPath.Contains(WipFolderPath);

        _checked = true;
    }
}
