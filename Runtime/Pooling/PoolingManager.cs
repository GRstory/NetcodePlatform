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
            Debug.LogError($"[PoolingManager] Addressable Key '{key}'�� �ش��ϴ� �������� �ε��� �� �����ϴ�.");
            _preparingKeys.Remove(key);
            Addressables.Release(handle); // �ε� ���� �� �ڵ� ��� ������
            _loadedPrefabHandles.Remove(key);
            return;
        }

        if (prefab.GetComponent<PoolableObject>() == null)
        {
            Debug.LogError($"[PoolingManager] '{prefab.name}' �����տ� PoolableObject ������Ʈ�� ���� Ǯ���� �� �����ϴ�.");
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
                    Debug.LogError($"[PoolingManager] '{addressableKey}' Ű�� Ǯ�� �������� ���߽��ϴ�. Get ��û�� ó���� �� �����ϴ�.");
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
            Debug.LogWarning($"[PoolingManager] '{obj.name}'�� Ǯ���� �����ϴ� ������Ʈ�� �ƴմϴ�. Destroy�մϴ�.");
            Destroy(obj);
            return;
        }

        if (poolable.IsPooled)
        {
            Debug.LogWarning($"[PoolingManager] '{obj.name}'�� �̹� Ǯ�� �ݳ��� ������Ʈ�Դϴ�. �ߺ� �ݳ��� �����մϴ�.");
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