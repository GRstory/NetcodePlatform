using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class PoolingManager : SingletonBehavior<PoolingManager>
{
    private Dictionary<string, Queue<GameObject>> _poolDict = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, AsyncOperationHandle<GameObject>> _loadedPrefabHandles = new Dictionary<string, AsyncOperationHandle<GameObject>>();
    private HashSet<string> _preparingKeys = new HashSet<string>();
    private Dictionary<string, Transform> _containerDict = new Dictionary<string, Transform>();
    private Transform _poolParent;

    private const int DEFAULT_POOL_SIZE = 5;

    protected override void Awake()
    {
        base.Awake();

        _poolParent = new GameObject("PoolParent").transform;
        _poolParent.parent = this.transform;

        PreparePoolsByLabelAsync("EssentialPooling");
    }

    public async Task PreparePoolsByLabelAsync(string label, int defaultPoolSize = 10)
    {
        var locationsHandle = Addressables.LoadResourceLocationsAsync(label, typeof(GameObject));
        IList<IResourceLocation> locations = await locationsHandle.Task;

        var prepareTasks = new List<Task>();
        foreach (var location in locations)
        {
            prepareTasks.Add(PreparePoolByKeyAsync(location.PrimaryKey, defaultPoolSize));
        }

        await Task.WhenAll(prepareTasks);
        Addressables.Release(locationsHandle);
    }

    public async Task PreparePoolByKeyAsync(string key, int size)
    {
        if (_poolDict.ContainsKey(key) || _preparingKeys.Contains(key)) return;

        _preparingKeys.Add(key);

        var handle = Addressables.LoadAssetAsync<GameObject>(key);
        _loadedPrefabHandles[key] = handle;

        GameObject prefab = await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded || prefab == null)
        {
            Debug.LogError($"[PoolingManager] Addressable Key '{key}'에 해당하는 프리팹을 로드할 수 없습니다.");
            _preparingKeys.Remove(key);
            Addressables.Release(handle); // 로드 실패 시 핸들 즉시 릴리즈
            _loadedPrefabHandles.Remove(key);
            return;
        }

        if (prefab.GetComponent<PoolableObject>() == null)
        {
            Debug.LogError($"[PoolingManager] '{prefab.name}' 프리팹에 PoolableObject 컴포넌트가 없어 풀링할 수 없습니다.");
            Addressables.Release(handle);
            _loadedPrefabHandles.Remove(key);
            _preparingKeys.Remove(key);
            return;
        }

        GameObject container = new GameObject($"{key}_Pool");
        container.transform.SetParent(_poolParent);
        _containerDict[key] = container.transform;

        _poolDict[key] = new Queue<GameObject>();

        for (int i = 0; i < size; i++)
        {
            GameObject obj = CreateInstance(key, prefab, false);
            _poolDict[key].Enqueue(obj);
        }

        _preparingKeys.Remove(key);
    }

    private GameObject CreateInstance(string key, GameObject prefab, bool isActive = true)
    {
        GameObject obj = Instantiate(prefab, _containerDict[key]);
        var poolable = obj.GetComponent<PoolableObject>();
        poolable.AddressableKey = key;
        obj.SetActive(isActive);
        return obj;
    }

    public async Task<GameObject> Get(string addressableKey, Vector3 position)
    {
        if (!_poolDict.ContainsKey(addressableKey))
        {
            while (_preparingKeys.Contains(addressableKey))
            {
                await Task.Yield();
            }

            if (!_poolDict.ContainsKey(addressableKey))
            {
                await PreparePoolByKeyAsync(addressableKey, DEFAULT_POOL_SIZE);

                if (!_poolDict.ContainsKey(addressableKey))
                {
                    Debug.LogError($"[PoolingManager] '{addressableKey}' 키의 풀을 생성하지 못했습니다. Get 요청을 처리할 수 없습니다.");
                    return null;
                }
            }
        }

        var poolQueue = _poolDict[addressableKey];
        GameObject objectToGet;

        if (poolQueue.Count == 0)
        {
            GameObject prefab = await _loadedPrefabHandles[addressableKey].Task;
            objectToGet = CreateInstance(addressableKey, prefab, true);
        }
        else
        {
            objectToGet = poolQueue.Dequeue();
        }

        PoolableObject poolableObject = objectToGet.GetComponent<PoolableObject>();
        poolableObject.IsPooled = false;
        objectToGet.SetActive(true);
        poolableObject.OnSpawn();
        poolableObject.transform.position = position;
        return objectToGet;
    }

    public Task<GameObject> Get(string addressableKey)
    {
        return Get(addressableKey, Vector3.zero);
    }

    public void Return(GameObject obj)
    {
        var poolable = obj.GetComponent<PoolableObject>();
        if (poolable == null || !_poolDict.ContainsKey(poolable.AddressableKey))
        {
            Debug.LogWarning($"[PoolingManager] '{obj.name}'은 풀에서 관리하는 오브젝트가 아닙니다. Destroy합니다.");
            Destroy(obj);
            return;
        }

        if (poolable.IsPooled)
        {
            Debug.LogWarning($"[PoolingManager] '{obj.name}'은 이미 풀에 반납된 오브젝트입니다. 중복 반납을 무시합니다.");
            return;
        }

        poolable.IsPooled = true;
        poolable.OnDespawn();

        var handle = _loadedPrefabHandles[poolable.AddressableKey];
        var prefabTransform = handle.Result.transform;

        obj.transform.SetParent(_containerDict[poolable.AddressableKey]);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = prefabTransform.localRotation;
        obj.transform.localScale = prefabTransform.localScale;

        obj.SetActive(false);
        _poolDict[poolable.AddressableKey].Enqueue(obj);
    }

    private void OnDestroy()
    {
        foreach (var handle in _loadedPrefabHandles.Values)
        {
            Addressables.Release(handle);
        }
        _loadedPrefabHandles.Clear();
        _poolDict.Clear();
        _containerDict.Clear();
    }
}