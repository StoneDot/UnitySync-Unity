using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class UnitySync : MonoBehaviour
{

    private static UnitySync instance;

    private UnitySyncConfiguration config;

    private Dictionary<uint, TransformSync> syncTargets;
    private ConcurrentQueue<WebSocketClient.UpdateObjectData> updateObjects;

    private WebSocketClient client;

    public static UnitySync GetInstance()
    {
        if (instance != null)
        {
            return instance;
        }
        else
        {
            GameObject go = new GameObject("Unity Sync");
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
        syncTargets = new Dictionary<uint, TransformSync>();
        updateObjects = new ConcurrentQueue<WebSocketClient.UpdateObjectData>();
        client = new WebSocketClient(config.Uri);
        client.OnObjectUpdated += Client_OnObjectUpdated;
    }

    private void Client_OnObjectUpdated(object sender, WebSocketClient.UpdateObjectData e)
    {
        updateObjects.Enqueue(e);
    }

    private void Start()
    {
        client.Connect();
    }

    private void Update()
    {
        while (!updateObjects.IsEmpty)
        {
            WebSocketClient.UpdateObjectData data;
            var success = updateObjects.TryDequeue(out data);
            if (!success) continue;
            var transform = syncTargets[data.ID].transform;
            transform.position = data.Position;
            transform.rotation = data.Rotation;
            transform.localScale = data.Scale;
            transform.hasChanged = false;
        }
    }

    public void TestConnect()
    {
        client.TestConnect();
    }

    public void RegisterTransformSync(TransformSync transformSync)
    {
        syncTargets.Add(transformSync.InstanceId, transformSync);
    }

    public void SendUpdate(TransformSync transformSync)
    {
        client.SendTransform(transformSync.InstanceId, transformSync.gameObject);
    }

    private void OnDestroy()
    {
        client.Disconnect();
    }
}
