using System.IO;
using UnityEditor;
using UnityEngine;

public class UnitySyncConfigurationEditor : ScriptableObject
{

    public const string ServerConfigPath = "Assets/UnitySync/Resources/Configuration/ServerConfiguration.asset";

    [MenuItem("UnitySync/Create Configuration")]
    private static void CreateUnitySyncConfigurationInstance()
    {
        Debug.Log(Utility.GetDirectoryName(ServerConfigPath));
        Utility.CreateDirectoryRecursive(Utility.GetDirectoryName(ServerConfigPath));
        if (!File.Exists(ServerConfigPath))
        {
            var asset = CreateInstance<UnitySyncConfiguration>();
            AssetDatabase.CreateAsset(asset, ServerConfigPath);
        }
        LastUsedInstanceId.GenerateResource();
        AssetDatabase.Refresh();
    }
}
