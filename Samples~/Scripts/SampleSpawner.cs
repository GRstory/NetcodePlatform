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
            // 2. PoolingManager�� ���� ������Ʈ �ν��Ͻ� ��������
            // Get�� �� ��° ������ position�� �߿����� �ʽ��ϴ�. �ٷ� �θ� �����ϰ� localPosition�� �ٲ� ���̱� �����Դϴ�.
            GameObject spawnedInstance = await PoolingManager.Instance.Get(prefabKey, Vector3.zero);

            if (spawnedInstance != null && parentObject != null)
            {
                // 3. �θ�-�ڽ� ���� �� ��� ��ġ/ȸ�� ����
                spawnedInstance.transform.SetParent(parentObject.transform);
                spawnedInstance.transform.localPosition = localPosition;
                spawnedInstance.transform.localRotation = localRotation;

                Debug.Log($"Ŭ���̾�Ʈ {NetworkManager.Singleton.LocalClientId}: {prefabKey} ���� �Ϸ�. �θ�: {parentObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"ID {parentNetworkId}�� �ش��ϴ� �θ� NetworkObject�� ã�� �� �����ϴ�.");
        }
    }
}
