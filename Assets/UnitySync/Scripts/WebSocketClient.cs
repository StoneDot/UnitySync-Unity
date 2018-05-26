using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Concurrent;

public class WebSocketClient
{
    private ClientWebSocket client;
    private CancellationToken connectToken;

    private ConcurrentQueue<PlacedObjectDataMapper> placedObjects;
    private ConcurrentQueue<UpdateObjectDataMapper> updatedObjects;
    private AutoResetEvent objectPushedEvent;

    public Uri Uri { get; private set; }
    public Guid Guid { get; private set; }
    
    private struct PlacedObjectData
    {
        public string Mode;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    private struct PlacedObjectDataMapper
    {
        public PlacedObjectData Data;
        public GameObject Target;
    }

    private struct UpdateObjectData
    {
        public string Mode;
        public int ID;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    private struct UpdateObjectDataMapper
    {
        public UpdateObjectData Data;
        public GameObject Target;
    }

    private struct EnterRoomDataMapper
    {
        public string Mode;
        public string RoomName;
        public string Guid;
    }

    public WebSocketClient(Uri uri)
    {
        Uri = uri;
        Guid = Guid.NewGuid();
        client = new ClientWebSocket();
        placedObjects = new ConcurrentQueue<PlacedObjectDataMapper>();
        updatedObjects = new ConcurrentQueue<UpdateObjectDataMapper>();
        objectPushedEvent = new AutoResetEvent(false);
    }

    public void Connect()
    {
        if (client.State == WebSocketState.Connecting) return;
        connectToken = new CancellationToken();
        var guiContext = SynchronizationContext.Current;
        Task.Run(async () =>
        {
            var taskContext = SynchronizationContext.Current;
            Debug.Log("Connecting");
            await client.ConnectAsync(Uri, connectToken);
            Debug.Log("Connected");
            Debug.Log("Entering Room");
            await EnterRoom();
            Debug.Log("Entered Room");

            while (client.State == WebSocketState.Open)
            {
                while (!placedObjects.IsEmpty)
                {
                    PlacedObjectDataMapper mapper;
                    if (placedObjects.TryDequeue(out mapper))
                    {
                        var data = GetByteArray(mapper.Data);
                        await client.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
                        Debug.Log("Placed");
                    }
                }
                while (!updatedObjects.IsEmpty)
                {
                    UpdateObjectDataMapper mapper;
                    if (updatedObjects.TryDequeue(out mapper))
                    {
                        var data = GetByteArray(mapper.Data);
                        await client.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
                        Debug.Log("Updated");
                    }
                }
                objectPushedEvent.WaitOne();
            }
        });
    }

    public void Disconnect()
    {
        client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal ending", CancellationToken.None);
    }

    private ArraySegment<byte> GetByteArray<T>(T obj)
    {
        var str = JsonUtility.ToJson(obj);
        var dataBytes = Encoding.UTF8.GetBytes(str);
        return new System.ArraySegment<byte>(dataBytes);
    }

    private PlacedObjectData GeneratePlacedObjectData(GameObject gameObject)
    {
        var transform = gameObject.transform;
        return new PlacedObjectData()
        {
            Mode = "PlaceObject",
            Position = transform.position,
            Rotation = transform.rotation,
            Scale = transform.localScale
        };
    }

    private UpdateObjectData GenerateUpdateObjectData(int id, GameObject gameObject)
    {
        var transform = gameObject.transform;
        return new UpdateObjectData()
        {
            Mode = "UpdateObject",
            ID = id,
            Position = transform.position,
            Rotation = transform.rotation,
            Scale = transform.localScale
        };
    }

    private Task EnterRoom(string roomName = "broadcast")
    {
        var obj = new EnterRoomDataMapper()
        {
            Mode = "EnterRoom",
            RoomName = roomName,
            Guid = Guid.ToString()
        };
        var data = GetByteArray(obj);
        return client.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <summary>
    /// 共有オブジェクトとして配置する。
    /// </summary>
    public void PlaceObject(GameObject gameObject)
    {
        var data = GeneratePlacedObjectData(gameObject);
        placedObjects.Enqueue(new PlacedObjectDataMapper()
        {
            Data = data,
            Target = gameObject
        });
        objectPushedEvent.Set();
    }

    /// <summary>
    /// すでに配置された共有オブジェクトの transform を更新する。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="gameObject"></param>
    public void SendTransform(int id, GameObject gameObject)
    {
        var data = GenerateUpdateObjectData(id, gameObject);
        updatedObjects.Enqueue(new UpdateObjectDataMapper()
        {
            Data = data,
            Target = gameObject
        });
        objectPushedEvent.Set();
    }

    public void TestConnect()
    {
        Task.Run(async () =>
        {
            Debug.Log("Starting connect");
            Debug.Log("State: " + client.State);
            var cancellationToken = new CancellationToken();
            Debug.Log("Support protocol: " + client.SubProtocol);
            var task = client.ConnectAsync(Uri, cancellationToken);
            Debug.Log("Connecting");
            Debug.Log("State: " + client.State);
            await task;
            Debug.Log("Connected");
            Debug.Log("State: " + client.State);
            string message = "Hello world!";
            var dataBytes = System.Text.Encoding.UTF8.GetBytes(message);
            var data = new System.ArraySegment<byte>(dataBytes);
            var task2 = client.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
            Debug.Log("Sendding");
            Debug.Log("State: " + client.State);
            await task2;
            Debug.Log("Sended");
            Debug.Log("State: " + client.State);
            var task3 = client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal ending", cancellationToken);
            Debug.Log("Closing");
            Debug.Log("State: " + client.State);
            await task3;
            Debug.Log("Closed");
            Debug.Log("State: " + client.State);
        });
    }
}
