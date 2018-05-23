using System;
using UnityEngine;

public class UnitySyncConfiguration : ScriptableObject
{

    [SerializeField]
    private string remoteServer = "127.0.0.1";

    [SerializeField]
    private ushort port = 3000;

    public string RemoteServer
    {
        get { return remoteServer; }
    }

    public ushort Port
    {
        get { return port; }
    }

    public Uri Uri { get; private set; }

    public UnitySyncConfiguration()
    {
        Uri = new Uri($"ws://{remoteServer}:{port}");
    }
    
}
