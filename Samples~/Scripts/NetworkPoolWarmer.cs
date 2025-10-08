using UnityEngine;
using Unity.Netcode;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

public class NetworkPoolWarmer : MonoBehaviour
{
    [System.Serializable]
    public class PoolablePrefab
    {
        public GameObject PrefabObject;
        public AssetReferenceGameObject PrefabAssetRef;
        public int InitialPoolSize = 10;
        public int RefillCount = 5;
    }

    [SerializeField] private List<PoolablePrefab> _prefabsToPool;
    public static bool IsReady { get; private set; } = false;

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            InitializeAndWarmupPools();
        }
    }

    private async void InitializeAndWarmupPools()
    {
        foreach (var prefabInfo in _prefabsToPool)
        {
            if (prefabInfo.PrefabObject == null || !prefabInfo.PrefabAssetRef.RuntimeKeyIsValid())
            {
                Debug.LogError("풀링할 프리팹 또는 어드레서블 참조가 등록되지 않았습니다.");
                continue;
            }

            string prefabKey = prefabInfo.PrefabAssetRef.RuntimeKey.ToString();
            var networkObject = prefabInfo.PrefabObject.GetComponent<NetworkObject>();

            var handler = new PooledPrefabInstanceHandler(prefabKey);
            handler.OnQueueLow += () => HandleQueueLow(handler, prefabKey, prefabInfo.RefillCount);

            if (NetworkManager.Singleton.PrefabHandler.AddHandler(networkObject, handler))
            {
                InGameManager.Instance.AddLog($"{NetworkManager.Singleton.LocalClientId} - [{prefabKey}] Add Handler", ELogLevel.Info);
            }
            else
            {
                InGameManager.Instance.AddLog($"{NetworkManager.Singleton.LocalClientId} - [{prefabKey}] Cant Add Handler", ELogLevel.Error);
            }

            for (int i = 0; i < prefabInfo.InitialPoolSize; i++)
            {
                GameObject instance = await PoolingManager.Instance.Get(prefabKey);
                if (instance != null)
                {
                    instance.SetActive(false);
                    handler.WarmupQueue(instance.GetComponent<NetworkObject>());
                }
            }
            IsReady = true;
            InGameManager.Instance.AddLog($"{NetworkManager.Singleton.LocalClientId} - [{prefabKey}] Finish Handler Queue", ELogLevel.Info);
        }
    }

    private async void HandleQueueLow(PooledPrefabInstanceHandler handler, string prefabKey, int amountToRefill)
    {
        InGameManager.Instance.AddLog($"{NetworkManager.Singleton.LocalClientId} - [{prefabKey}] Low Pooling Queue - refill {amountToRefill}", ELogLevel.Info);
        for (int i = 0; i < amountToRefill; i++)
        {
            GameObject instance = await PoolingManager.Instance.Get(prefabKey);
            if (instance != null)
            {
                instance.SetActive(false);
                handler.WarmupQueue(instance.GetComponent<NetworkObject>());
            }
        }
    }
}