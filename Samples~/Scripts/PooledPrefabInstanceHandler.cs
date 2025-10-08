using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;

public class PooledPrefabInstanceHandler : INetworkPrefabInstanceHandler
{
    public event Action OnQueueLow;

    private string _prefabAddressableKey;
    private Queue<NetworkObject> _instanceQueue = new Queue<NetworkObject>();
    private const int QUEUE_LOW_THRESHOLD = 5;

    public PooledPrefabInstanceHandler(string prefabAddressableKey)
    {
        _prefabAddressableKey = prefabAddressableKey;
    }

    public void WarmupQueue(NetworkObject instance)
    {
        _instanceQueue.Enqueue(instance);
    }

    public void Destroy(NetworkObject networkObject)
    {
        PoolingManager.Instance.Return(networkObject.gameObject);
    }

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        if (_instanceQueue.Count > 0)
        {
            NetworkObject instance = _instanceQueue.Dequeue();

            if (_instanceQueue.Count <= QUEUE_LOW_THRESHOLD)
            {
                OnQueueLow?.Invoke();
            }

            instance.gameObject.SetActive(true);
            instance.transform.position = position;
            instance.transform.rotation = rotation;

            return instance;
        }

        Debug.LogError($"[{_prefabAddressableKey}] 풀이 완전히 비었습니다! NetworkPoolWarmer의 초기 PoolSize를 늘리거나, 보충 속도보다 스폰 속도가 너무 빠릅니다.");
        return null;
    }
}