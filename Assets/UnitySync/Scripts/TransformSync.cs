using UnityEngine;

public class TransformSync : MonoBehaviour
{
    private UnitySync unitySync;

    [SerializeField, ReadOnly]
    private bool defaultPlaced = false;

    [SerializeField, ReadOnly]
    private uint _instanceId = 0;
    public uint InstanceId
    {
        get { return _instanceId; }
    }

    private void Start()
    {
        unitySync = UnitySync.GetInstance();
        unitySync.RegisterTransformSync(this);
    }

    private void Update()
    {
        unitySync.SendUpdate(this);
    }

#if UNITY_EDITOR

    private void Reset()
    {
        LastUsedInstanceId.IncrementUsedId();
        _instanceId = LastUsedInstanceId.GetLastUsedId();
        defaultPlaced = true;
    }

#endif
}
