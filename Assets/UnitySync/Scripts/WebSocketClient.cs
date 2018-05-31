using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp;

public class WebSocketClient : IDisposable
{
    private WebSocket ws;

    private ConcurrentQueue<PlacedObjectDataMapper> placedObjects;
    private ConcurrentQueue<UpdateObjectDataMapper> updatedObjects;
    private AutoResetEvent objectPushedEvent;
    private bool terminate;
    private bool needReconnect;

    public Uri Uri { get; private set; }
    public ushort UserId { get; private set; }

    public int TimeoutMilliseconds { get; set; } = 3000;

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
        placedObjects = new ConcurrentQueue<PlacedObjectDataMapper>();
        updatedObjects = new ConcurrentQueue<UpdateObjectDataMapper>();
        objectPushedEvent = new AutoResetEvent(false);
        terminate = false;
        needReconnect = false;
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
        Task.Run(() =>
        {
            while (!terminate)
            {
                needReconnect = false;

                Debug.Log($"Connecting: {Uri.AbsoluteUri}");
                ws = new WebSocket(Uri.AbsoluteUri);
                ws.OnOpen += (sender, e) =>
                {
                    Debug.Log("Connected WebSocket");
                };
                ws.OnClose += (sender, e) =>
                {
                    Debug.Log("Closed WebSocket");

                    needReconnect = true;
                    objectPushedEvent.Set();
                };
                ws.OnError += (sender, e) =>
                {
                    Debug.Log("Error occur");
                    Debug.Log(e.Exception.ToString());
                };
                ws.OnMessage += OnMessage;

                Debug.Log("Start connect");
                ws.Connect();
                Debug.Log("End connect");

                EnterRoom();

                SendingLoop();
            }
            Debug.Log("Finish client");
        });
    }

    private void OnMessage(object sender, MessageEventArgs e)
    {
        if (e.IsText)
        {
            var mode = ExtractJsonMode(e.Data);
            switch (mode)
            {
                case "PlaceObject":
                    var placedData = JsonUtility.FromJson<PlacedObjectData>(e.Data);
                    break;
                case "UpdateObject":
                    var updatedData = JsonUtility.FromJson<UpdateObjectData>(e.Data);
                    OnObjectUpdated?.Invoke(this, updatedData);
                    break;
                default:
                    Debug.LogError("Something wrong");
                    break;
            }
        }
        else if (e.IsBinary)
        {
        }
    }

    private void SendingLoop()
    {
        try
        {
            while (ws.IsAlive && !needReconnect)
            {
                while (!placedObjects.IsEmpty)
                {
                    PlacedObjectDataMapper mapper;
                    if (placedObjects.TryDequeue(out mapper))
                    {
                        if (ws.IsAlive)
                        {
                            ws.SendAsync(JsonUtility.ToJson(mapper.Data), (c) => { });
                        }
                    }
                }
                while (!updatedObjects.IsEmpty)
                {
                    UpdateObjectDataMapper mapper;
                    if (updatedObjects.TryDequeue(out mapper))
                    {
                        if (ws.IsAlive)
                        {
                            ws.SendAsync(JsonUtility.ToJson(mapper.Data), (c) => { });
                        }
                    }
                }
                objectPushedEvent.WaitOne();
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());

            // Force ListeningLoop to quit
            needReconnect = true;
        }
        Debug.Log("End TX loop");
    }

    public void Disconnect()
    {
        ws.Close();
        terminate = true;
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

    private void EnterRoom(string roomName = "broadcast")
    {
        var obj = new EnterRoomData()
        {
            Mode = "EnterRoom",
            RoomName = roomName,
        };
        Debug.Log($"Entering room ({roomName})");
        if (ws.IsAlive)
        {
            ws.Send(JsonUtility.ToJson(obj));
            Debug.Log($"Entered room ({roomName})");
        }
        else
        {
            Debug.Log($"Failed to enter room ({roomName})");
        }
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

    public void Dispose()
    {
        ws.Close();
    }
}
