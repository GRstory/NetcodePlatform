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

    private void Awake()
    {
        PlayerDatasInGame = new NetworkList<PlayerData>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
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
}
