using UnityEditor;
using UnityEngine;

public class UnitySyncConfigurationEditor : ScriptableObject
{

    [MenuItem("UnitySync/Create Configuration")]
    private static void CreateUnitySyncConfigurationInstance()
    {
        var asset = CreateInstance<UnitySyncConfiguration>();
        if (!AssetDatabase.IsValidFolder("Assets/UnitySync"))
            AssetDatabase.CreateFolder("Assets", "UnitySync");
        if (!AssetDatabase.IsValidFolder("Assets/UnitySync/Resources"))
            AssetDatabase.CreateFolder("Assets/UnitySync", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/UnitySync/Resources/Configuration"))
            AssetDatabase.CreateFolder("Assets/UnitySync/Resources", "Configuration");
        AssetDatabase.CreateAsset(asset, "Assets/UnitySync/Resources/Configuration/ServerConfiguration.asset");
        AssetDatabase.Refresh();
    }
}
