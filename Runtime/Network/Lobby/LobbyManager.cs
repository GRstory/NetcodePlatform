using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class LobbyManager : SingletonNetwork<LobbyManager>
{
    [SerializeField] private GameObject _gameSessionSettingsPrefab;
    public NetworkList<PlayerData> PlayerDataList { get; private set; }
    public int CurrentPlayerCount => PlayerDataList.Count;

    public event Action<ELobbyState, string> OnLobbyStateChanged;
    public int MaxPlayer = 4;

    private bool _isInitialized = false;
    private bool _isKicked = false;
    private string _playerNameCache = "";

    protected override void Awake()
    {
        base.Awake();

        PlayerDataList = new NetworkList<PlayerData>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
            );

    }

    private async void Start()
    {
        await Task.Yield();

        if(await InitializeUnityServicesAsync())
        {
            _isInitialized = true;
        }
        else
        {
            OnLobbyStateChanged?.Invoke(ELobbyState.Error, "Unity 서비스 초기화 실패.");
        }
    }

    #region NetworkBehaviour
    public override void OnNetworkSpawn()
    {
        bool isReturningFromGame = false;
        if (GameSessionSettings.Instance != null && GameSessionSettings.Instance.PlayerDatasInGame.Count > 0)
        {
            isReturningFromGame = true;
        }

        if(IsServer)
        {
            if(isReturningFromGame)
            {
                RestorePlayerListFromSession();
                OnLobbyStateChanged?.Invoke(ELobbyState.HostSuccess, GameSessionSettings.Instance.JoinCode);
            }
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
        if(IsClient && !IsServer)
        {
            if (isReturningFromGame)
            {
                OnLobbyStateChanged?.Invoke(ELobbyState.ClientSuccess, "Returned to Lobby");
            }
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect_OnClient;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected_OnClient;
        }
    }

    public override void OnNetworkDespawn()
    {
        if(IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
        if (IsClient)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect_OnClient;
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected_OnClient;
        }
    }
    #endregion

    #region LobbyFunction
    private async Task<bool> InitializeUnityServicesAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (AuthenticationService.Instance.IsSignedIn == false)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return false;
        }
    }

    public async Task CreateLobby()
    {
        if (!_isInitialized) return;

        OnLobbyStateChanged?.Invoke(ELobbyState.Connecting, "로비 생성 중...");
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayer);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            GUIUtility.systemCopyBuffer = joinCode;

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            if(PlayerDataList.Count > 0) PlayerDataList.Clear();

            NetworkManager.Singleton.StartHost();

            GameObject gssObject = GameObject.Instantiate(_gameSessionSettingsPrefab);
            GameSessionSettings gss = gssObject.GetComponent<GameSessionSettings>();
            gss.MaxPlayerCount = MaxPlayer;
            gss.JoinCode = joinCode;
            gss.IsSessionHost = true;
            gssObject.GetComponent<NetworkObject>().Spawn();

            OnLobbyStateChanged?.Invoke(ELobbyState.HostSuccess, joinCode);
        }
        catch (Exception e)
        {
            Debug.LogError($"로비 생성 실패: {e.Message}");
            OnLobbyStateChanged?.Invoke(ELobbyState.Error, "로비 생성에 실패했습니다.");
        }
    }

    public async Task JoinLobby(string joinCode)
    {
        if (!_isInitialized) return;

        OnLobbyStateChanged?.Invoke(ELobbyState.Connecting, "로비 참가 중...");
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e)
        {
            Debug.LogError($"로비 참가 실패: {e.Message}");
            OnLobbyStateChanged?.Invoke(ELobbyState.Error, "invalid code or unconnectable lobby");
        }
    }

    public void StartGame()
    {
        if (!IsServer) return;

        if(GameSessionSettings.Instance.SelectedGameMode.Value == EGameModeType.Default)
        {
            OnLobbyStateChanged?.Invoke(ELobbyState.None, "Select valid gamemode");
            return;
        }

        GameSessionSettings.Instance.IsGameStarted.Value = true;
        GameSessionSettings.Instance.PlayerDatasInGame.Clear();
        foreach (PlayerData data in PlayerDataList)
        {
            GameSessionSettings.Instance.PlayerDatasInGame.Add(data);
        }
        NetworkManager.Singleton.SceneManager.LoadScene("SampleGameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void KickPlayer(ulong clientId)
    {
        if (!IsServer) return;
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        KickClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        NetworkManager.Singleton.DisconnectClient(clientId);
    }

    public void ShutdownLobby()
    {
        if(!IsServer) return;
        Debug.Log("ShutdownLobby");

        NetworkManager.Singleton.Shutdown();
        PlayerDataList.Clear();
        OnLobbyStateChanged?.Invoke(ELobbyState.Idle, "Lobby was shutdowned");
    }

    public void LeaveLobby()
    {
        if (!IsClient || IsHost) return;
        Debug.Log("LeaveLobby");
        NetworkManager.Singleton.Shutdown();
    }

    public void SetPlayerName(string name)
    {
        Debug.Log("SetPlayerName");
        _playerNameCache = name;
        SetNicknameServerRpc(name);
    }

    private void RestorePlayerListFromSession()
    {
        Debug.Log("RestorePlayerListFromSession");
        PlayerDataList.Clear();
        foreach (PlayerData data in GameSessionSettings.Instance.PlayerDatasInGame)
        {
            PlayerDataList.Add(data);
        }

        if(IsServer)
        {
            OnLobbyStateChanged?.Invoke(ELobbyState.HostSuccess, GameSessionSettings.Instance.JoinCode);
        }
        else if(IsClient)
        {
            OnLobbyStateChanged?.Invoke(ELobbyState.ClientSuccess, "Lobby Reconnected");
        }
        GameSessionSettings.Instance.PlayerDatasInGame.Clear();
    }
    #endregion

    #region Handler
    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log("HandleClientConnected");
        PlayerDataList.Add(new PlayerData { ClientId = clientId, PlayerName = $"Player{clientId}" });
    }

    private void HandleClientConnected_OnClient(ulong clientId)
    {
        Debug.Log("HandleClientConnected_OnClient");
        //미리 지정된 이름으로 변경
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            OnLobbyStateChanged?.Invoke(ELobbyState.ClientSuccess, "connected");
            SetPlayerName(_playerNameCache);
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log("HandleClientDisconnected");
        for (int i = 0; i < PlayerDataList.Count; i++)
        {
            if( PlayerDataList[i].ClientId == clientId )
            {
                PlayerDataList.RemoveAt(i);
                break;
            }
        }
    }

    private void HandleClientDisconnect_OnClient(ulong clientId)
    {
        Debug.Log("HandleClientDisconnect_OnClient");
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        if (_isKicked)
        {
            OnLobbyStateChanged?.Invoke(ELobbyState.Idle, "You have been kicked by host");
        }
        else
        {
            OnLobbyStateChanged?.Invoke(ELobbyState.Idle, "Lobby is full or unavailable");
        }

        _isKicked = false;
    }
    #endregion

    #region RPC
    [ServerRpc(RequireOwnership = false)]
    private void SetNicknameServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrEmpty(name)) return;

        //닉네임 설정
        ulong clientId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < PlayerDataList.Count; i++)
        {
            if (PlayerDataList[i].ClientId == clientId)
            {
                PlayerData data = PlayerDataList[i];
                data.PlayerName = name;
                PlayerDataList[i] = data;
                break;
            }
        }
    }

    [ClientRpc]
    private void KickClientRpc(ClientRpcParams rpcParams = default)
    {
        _isKicked = true;
    }
    #endregion
}

[Serializable]
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId && PlayerName.Equals(other.PlayerName);
    }
}