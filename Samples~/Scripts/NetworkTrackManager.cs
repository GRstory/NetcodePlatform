using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;

public class NetworkTrackManager : NetworkBehaviour
{
    public static NetworkTrackManager Instance { get; private set; }

    [Header("Track Core Settings")]
    [SerializeField] private Transform _trackContainer;
    [SerializeField] private float _spawnAheadDistance = 150f;

    [Header("Track Speed Settings")]
    [SerializeField] private float _defaultTrackSpeed = 15f;

    [Header("Ground Prefab")]
    [SerializeField] private GameObject _groundPrefabObject;

    [Header("Sync Settings")]
    [Tooltip("������ Ŭ���̾�Ʈ���� ��ġ�� �󸶳� ���� ������ (��)")]
    [SerializeField] private float _positionSyncInterval = 0.1f;
    [Tooltip("���� ��ġ�� �����ϴ� �ӵ�")]
    [SerializeField] private float _clientLerpSpeed = 20f;

    // --- ���� ���� ���� ---
    private NetworkObject _lastSpawnedGround = null;
    private bool _isServerInitialized = false;
    private readonly List<NetworkObject> _activeGrounds = new List<NetworkObject>();
    private Coroutine _syncCoroutine;
    private float _currentTrackSpeed;

    // --- Ŭ���̾�Ʈ ���� ���� ---
    private struct StateSnapshot
    {
        public double Time;
        public Vector3 Position;
    }
    private readonly List<StateSnapshot> _positionBuffer = new List<StateSnapshot>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            _currentTrackSpeed = _defaultTrackSpeed;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            StartTrack();
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            MoveTrack();
            if (!_isServerInitialized) return;

            float trackFrontZ = _lastSpawnedGround != null ? _lastSpawnedGround.transform.position.z + (_lastSpawnedGround.GetComponent<BoxCollider>().size.z * _lastSpawnedGround.transform.lossyScale.z / 2f) : 0;
            if (trackFrontZ < _spawnAheadDistance)
            {
                SpawnNextGround();
            }
        }
        else
        {
            // --- Ŭ���̾�Ʈ ���� ���� ---
            if (_positionBuffer.Count < 2) return; // ������ ���� �����Ͱ� �ּ� 2�� �ʿ�

            StateSnapshot latest = _positionBuffer[_positionBuffer.Count - 1];
            StateSnapshot previous = _positionBuffer[_positionBuffer.Count - 2];

            // �� ���� ������ �ð��� ��ġ ��ȭ�� ���� ������ ���� �ӵ��� ���
            double timeDiff = latest.Time - previous.Time;
            if (timeDiff <= 0) return;
            Vector3 velocity = (latest.Position - previous.Position) / (float)timeDiff;

            // ���������� ���� ��ġ����, ������ ��Ŷ ���� �帥 �ð���ŭ�� �����Ͽ� ������
            // �̰��� ������ '���� ���� ��ġ'�� ��
            float timeSinceLastPacket = (float)(NetworkManager.Singleton.ServerTime.Time - latest.Time);
            Vector3 targetPosition = latest.Position + velocity * timeSinceLastPacket;

            // Ʈ�� �����̳ʸ� �� '������ ���� ��ġ'�� �ε巴�� �̵�
            _trackContainer.position = Vector3.Lerp(_trackContainer.position, targetPosition, Time.deltaTime * _clientLerpSpeed);
        }
    }

    private void MoveTrack()
    {
        if (!IsServer) return;
        _trackContainer.Translate(Vector3.back * _currentTrackSpeed * Time.deltaTime, Space.World);
    }

    private IEnumerator SyncContainerPositionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_positionSyncInterval);
            UpdateContainerPositionClientRpc(_trackContainer.position, NetworkManager.Singleton.ServerTime.Time);
        }
    }

    [ClientRpc]
    private void UpdateContainerPositionClientRpc(Vector3 serverPosition, double serverTime, ClientRpcParams clientRpcParams = default)
    {
        if (IsServer) return;

        // ���� ��ġ�� �ð��� ���ۿ� �߰�
        _positionBuffer.Add(new StateSnapshot { Time = serverTime, Position = serverPosition });

        if (_positionBuffer.Count > 20)
        {
            _positionBuffer.RemoveAt(0);
        }
    }

    #region Unchanged Code
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsServer)
        {
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            if (_syncCoroutine != null) StopCoroutine(_syncCoroutine);
        }
    }
    private void HandleClientConnected(ulong clientId)
    {
        ClientRpcParams clientRpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } };
        UpdateContainerPositionClientRpc(_trackContainer.position, NetworkManager.Singleton.ServerTime.Time, clientRpcParams);
        foreach (var ground in _activeGrounds)
        {
            if (ground != null) UpdateGroundPositionClientRpc(ground.NetworkObjectId, ground.transform.localPosition, clientRpcParams);
        }
    }
    private void SpawnNextGround()
    {
        if (!IsServer || _groundPrefabObject == null) return;
        GameObject groundInstance = Instantiate(_groundPrefabObject);
        NetworkObject networkObject = groundInstance.GetComponent<NetworkObject>();
        networkObject.Spawn(true);
        networkObject.TrySetParent(_trackContainer);
        var groundCollider = groundInstance.GetComponent<BoxCollider>();
        float groundLength = groundCollider.size.z * groundInstance.transform.localScale.z;
        Vector3 finalLocalPosition;
        if (_lastSpawnedGround == null) finalLocalPosition = new Vector3(0, 0, groundLength / 2f);
        else
        {
            var lastGroundCollider = _lastSpawnedGround.GetComponent<BoxCollider>();
            float lastGroundLength = lastGroundCollider.size.z * _lastSpawnedGround.transform.localScale.z;
            float lastGroundFrontEdge = _lastSpawnedGround.transform.localPosition.z + (lastGroundLength / 2f);
            float newGroundCenter = lastGroundFrontEdge + (groundLength / 2f);
            finalLocalPosition = new Vector3(0, 0, newGroundCenter);
        }
        groundInstance.transform.localPosition = finalLocalPosition;
        groundInstance.transform.localRotation = Quaternion.identity;
        InGameManager.Instance.AddLog($"TrackContainerPosition: {_trackContainer.position.z}");
        UpdateGroundPositionClientRpc(networkObject.NetworkObjectId, finalLocalPosition);
        _lastSpawnedGround = networkObject;
        _activeGrounds.Add(networkObject);
    }

    [ClientRpc]
    private void UpdateGroundPositionClientRpc(ulong networkObjectId, Vector3 localPosition, ClientRpcParams clientRpcParams = default)
    {
        if (IsServer) return;
        InGameManager.Instance.AddLog($"TrackContainerPosition: {_trackContainer.position.z}");
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject targetObject))
        {
            targetObject.transform.localPosition = localPosition;
        }
    }

    private void StartTrack()
    {
        if (!IsServer) return;
        _lastSpawnedGround = null;
        _activeGrounds.Clear();
        if (_syncCoroutine != null) StopCoroutine(_syncCoroutine);
        _syncCoroutine = StartCoroutine(SyncContainerPositionRoutine());
        InitializeGrounds();
    }

    private async void InitializeGrounds()
    {
        await Task.Delay(500);
        float currentTrackLength = 0;
        while (currentTrackLength < _spawnAheadDistance)
        {
            SpawnNextGround();
            if (_lastSpawnedGround != null)
            {
                var lastCollider = _lastSpawnedGround.GetComponent<BoxCollider>();
                currentTrackLength = _lastSpawnedGround.transform.localPosition.z + (lastCollider.size.z * _lastSpawnedGround.transform.localScale.z / 2f);
            }
        }
        _isServerInitialized = true;
    }
    #endregion
}