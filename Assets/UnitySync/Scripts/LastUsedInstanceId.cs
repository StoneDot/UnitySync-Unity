using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class LastUsedInstanceId : ScriptableObject
{
    public uint InstanceId = 0;

    public const string ResourcePath = "Assets/UnitySync/Resources/Configuration/LastUsedId.asset";

    private static LastUsedInstanceId _instance;

    private static LastUsedInstanceId GetFromResource()
    {
        return Resources.Load<LastUsedInstanceId>(Utility.AssetPathToLoadDirectory(ResourcePath));
    }

    private static LastUsedInstanceId GetInstance()
    {
        if (_instance == null)
        {
            GenerateResource();
            _instance = GetFromResource();
        }
        return _instance;
    }

    public static uint GetLastUsedId()
    {
        return GetInstance().InstanceId;
    }

    public static void IncrementUsedId()
    {
        GetInstance().InstanceId++;
    }

    public static void GenerateResource()
    {
#if UNITY_EDITOR
        Utility.CreateDirectoryRecursive(Utility.GetDirectoryName(ResourcePath));
        if (!File.Exists(ResourcePath))
        {
            var asset = CreateInstance<LastUsedInstanceId>();
            AssetDatabase.CreateAsset(asset, ResourcePath);
        }
#endif
    }
}
