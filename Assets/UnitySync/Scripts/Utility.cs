using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class Utility
{
    public static void CreateDirectoryRecursive(string path)
    {
#if UNITY_EDITOR
        path = path.TrimEnd('/');
        var currentPath = "";
        foreach (var name in path.Split(new char[] { '/' }))
        {
            if (!AssetDatabase.IsValidFolder(currentPath + '/' + name))
            {
                AssetDatabase.CreateFolder(currentPath, name);
            }
            if (currentPath == string.Empty)
            {
                currentPath += name;
            }
            else
            {
                currentPath += '/' + name;
            }
        }
#endif
    }

    public static string GetDirectoryName(string filePath)
    {
        return Path.GetDirectoryName(filePath).Replace('\\', '/');
    }

    public static string AssetPathToLoadDirectory(string assetPath)
    {
        assetPath = assetPath.Remove(0, assetPath.IndexOf("Resources/") + "Resources/".Length);
        return assetPath.Remove(assetPath.Length - ".asset".Length);
    }
}