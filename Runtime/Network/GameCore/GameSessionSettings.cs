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
    public string JoinCode = "";
    public bool IsSessionHost = false;

    public List<string> PlayerLameListTest = new List<string>();

    private void Awake()
    {
        PlayerDatasInGame = new NetworkList<PlayerData>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += HandleConnectionApprovalCheck;
        }
    }

    public override void OnNetworkDespawn()
    {
        if(IsServer)
        {
            if(NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback -= HandleConnectionApprovalCheck;
            }
        }
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

    public bool TryGetClientIdByNickname(string nickname, out ulong clientId)
    {
        foreach (PlayerData playerData in PlayerDatasInGame)
        {
            if (playerData.PlayerName == nickname)
            {
                clientId = playerData.ClientId;
                return true;
            }
        }

        clientId = ulong.MaxValue;
        return false;
    }
}
