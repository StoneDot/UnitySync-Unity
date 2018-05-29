using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class WebSocketClient
{
    private ClientWebSocket client;
    private CancellationToken connectToken;

    private ConcurrentQueue<PlacedObjectDataMapper> placedObjects;
    private ConcurrentQueue<UpdateObjectDataMapper> updatedObjects;
    private AutoResetEvent objectPushedEvent;

    public Uri Uri { get; private set; }
    public ushort UserId { get; private set; }

    public event EventHandler<UpdateObjectData> OnObjectUpdated;

    public struct PlacedObjectData
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

    public struct UpdateObjectData
    {
        public string Mode;
        public uint ID;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    private struct UpdateObjectDataMapper
    {
        public UpdateObjectData Data;
        public GameObject Target;
    }

    private struct EnterRoomData
    {
        public string Mode;
        public string RoomName;
    }

    public WebSocketClient(Uri uri)
    {
        Uri = uri;
        ResetUserId();
        client = new ClientWebSocket();
        placedObjects = new ConcurrentQueue<PlacedObjectDataMapper>();
        updatedObjects = new ConcurrentQueue<UpdateObjectDataMapper>();
        objectPushedEvent = new AutoResetEvent(false);
    }

    private void ResetUserId()
    {
        var randomByte = new byte[2];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(randomByte);
        }
        UserId = BitConverter.ToUInt16(randomByte, 0);
    }

    public void Connect()
    {
        if (client.State == WebSocketState.Connecting) return;
        connectToken = new CancellationToken();
        Task.Run(async () =>
        {
            Debug.Log("Connecting");
            await client.ConnectAsync(Uri, connectToken);
            Debug.Log("Connected");
            Debug.Log("Entering Room");
            await EnterRoom();
            Debug.Log("Entered Room");

            var rxTask = Task.Run(async () =>
            {
                const int DefaultBufferSize = 1024;
                var rxBuff = new byte[DefaultBufferSize];
                var rxData = new ArraySegment<byte>(rxBuff);
                var data = new List<byte>(DefaultBufferSize);
                try
                {
                    while (client.State == WebSocketState.Open)
                    {
                        var result = await client.ReceiveAsync(rxData, CancellationToken.None);
                        // TODO: 最適化ができるかも
                        // https://stackoverflow.com/questions/23413068/fast-way-to-copy-an-array-into-a-list
                        var clippedData = new ArraySegment<byte>(rxData.Array, 0, result.Count);
                        data.AddRange(clippedData);
                        if (!result.EndOfMessage) continue;
                        switch (result.MessageType)
                        {
                            case WebSocketMessageType.Text:
                                var jsonString = Encoding.UTF8.GetString(data.ToArray(), 0, data.Count);
                                var mode = ExtractJsonMode(jsonString);
                                //Debug.Log("JSON come: " + jsonString);
                                switch (mode)
                                {
                                    case "PlaceObject":
                                        var placedData = JsonUtility.FromJson<PlacedObjectData>(jsonString);
                                        break;
                                    case "UpdateObject":
                                        var updatedData = JsonUtility.FromJson<UpdateObjectData>(jsonString);
                                        OnObjectUpdated?.Invoke(this, updatedData);
                                        break;
                                    default:
                                        Debug.LogError("Something wrong");
                                        break;
                                }
                                break;
                            case WebSocketMessageType.Binary:
                                break;
                            case WebSocketMessageType.Close:
                                break;
                        }
                        data.Clear();
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e.ToString());
                }
                Debug.Log("End RX loop");
            });

            while (client.State == WebSocketState.Open)
            {
                while (!placedObjects.IsEmpty)
                {
                    PlacedObjectDataMapper mapper;
                    if (placedObjects.TryDequeue(out mapper))
                    {
                        var data = GetByteArray(mapper.Data);
                        await client.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                while (!updatedObjects.IsEmpty)
                {
                    UpdateObjectDataMapper mapper;
                    if (updatedObjects.TryDequeue(out mapper))
                    {
                        var data = GetByteArray(mapper.Data);
                        await client.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                objectPushedEvent.WaitOne();
            }

            Debug.Log("Waiting finishing rxTask");
            await rxTask;
            Debug.Log("Closed websocket connection.");
        });
    }

    public void Disconnect()
    {
        client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal ending", CancellationToken.None);
    }

    private static Regex modeRegex = new Regex("\"Mode\":\"([^\"]*?)\"");
    private string ExtractJsonMode(string jsonString)
    {
        var match = modeRegex.Match(jsonString);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        else
        {
            return "None";
        }
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

    private UpdateObjectData GenerateUpdateObjectData(uint id, GameObject gameObject)
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
        var obj = new EnterRoomData()
        {
            Mode = "EnterRoom",
            RoomName = roomName,
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
    public void SendTransform(uint id, GameObject gameObject)
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
