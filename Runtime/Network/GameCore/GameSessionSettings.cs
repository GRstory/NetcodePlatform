using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameSessionSettings : NetworkBehaviour
{
    public static GameSessionSettings Instance;

    public NetworkVariable<EGameModeType> SelectedGameMode = new NetworkVariable<EGameModeType>(EGameModeType.Default);
    public NetworkVariable<bool> IsGameStarted = new NetworkVariable<bool>(false);
    public NetworkList<PlayerData> PlayerDatasInGame { get; private set; }
    public int MaxPlayerCount = 0;

    private void Awake()
    {
        PlayerDatasInGame = new NetworkList<PlayerData>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    private void Start()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback += HandleConnectionApprovalCheck;
    }

    public override void OnDestroy()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback -= HandleConnectionApprovalCheck;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if(Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void HandleConnectionApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (IsGameStarted.Value)
        {
            response.Reason = "Game has already started.";
            response.Approved = false;
        }
        else if(LobbyManager.Instance.CurrentPlayerCount >= MaxPlayerCount)
        {
            response.Reason = "Lobby is full.";
            response.Approved = false;
        }
        else
        {
            response.Approved = true;
        }
    }
}
