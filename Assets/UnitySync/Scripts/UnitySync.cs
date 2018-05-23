using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitySync : MonoBehaviour
{

    private static UnitySync instance;

    private UnitySyncConfiguration config;

    public WebSocketClient Client { get; private set; }

    public static UnitySync GetInstance()
    {
        if (instance != null)
        {
            return instance;
        }
        else
        {
            GameObject go = new GameObject();
            DontDestroyOnLoad(go);
            instance = go.AddComponent<UnitySync>();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        config = Resources.Load<UnitySyncConfiguration>("Configuration/ServerConfiguration");
        Client = new WebSocketClient(config.Uri);
    }

    private void Start()
    {
        Client.Connect();
    }

    public void TestConnect()
    {
        Client.TestConnect();
    }
}
