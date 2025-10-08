using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.AddressableAssets;

public class SampleSpawner : NetworkBehaviour
{
    [SerializeField] private LayerMask _groudMask;
    [SerializeField] private bool _spawn = false;
    [SerializeField] private AssetReferenceGameObject _samplePrefab;

    private string _samplePrefabKey;

    private void Awake()
    {
        _samplePrefabKey = _samplePrefab.RuntimeKey.ToString();
    }

    private void OnValidate()
    {
        if (!IsServer) return;
        if(_spawn)
        {
            _spawn = false;
            SpawnObjectAtPosition(_samplePrefabKey, new Vector3(0, 0, 0));
        }
    }


    public void SpawnObjectAtPosition(string key, Vector3 worldPos)
    {
        if (!IsServer) return;
        Debug.DrawRay(worldPos + Vector3.up * 5f, Vector3.down * 10f, Color.red, 2f);
        if (Physics.Raycast(worldPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 100f, _groudMask))
        {
            NetworkObject parentObject = hit.collider.GetComponent<NetworkObject>();
            if(parentObject != null)
            {
                Transform parentTransform = parentObject.transform;
                Vector3 localPosition = parentTransform.InverseTransformPoint(worldPos);
                Quaternion localRotation = Quaternion.Inverse(parentTransform.rotation) * Quaternion.identity;

                SpawnObjectOnParentClientRpc(key, parentObject.NetworkObjectId, localPosition, localRotation);
            }
        }
    }

    [ClientRpc]
    private void SpawnObjectOnParentClientRpc(string prefabKey, ulong parentNetworkId, Vector3 localPosition, Quaternion localRotation)
    {
        SpawnLogic(prefabKey, parentNetworkId, localPosition, localRotation);
    }

    private async void SpawnLogic(string prefabKey, ulong parentNetworkId, Vector3 localPosition, Quaternion localRotation)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(parentNetworkId, out NetworkObject parentObject))
        {
            // 2. PoolingManager를 통해 오브젝트 인스턴스 가져오기
            // Get의 두 번째 인자인 position은 중요하지 않습니다. 바로 부모를 설정하고 localPosition을 바꿀 것이기 때문입니다.
            GameObject spawnedInstance = await PoolingManager.Instance.Get(prefabKey, Vector3.zero);

            if (spawnedInstance != null && parentObject != null)
            {
                // 3. 부모-자식 관계 및 상대 위치/회전 설정
                spawnedInstance.transform.SetParent(parentObject.transform);
                spawnedInstance.transform.localPosition = localPosition;
                spawnedInstance.transform.localRotation = localRotation;

                Debug.Log($"클라이언트 {NetworkManager.Singleton.LocalClientId}: {prefabKey} 스폰 완료. 부모: {parentObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"ID {parentNetworkId}에 해당하는 부모 NetworkObject를 찾을 수 없습니다.");
        }
    }
}
