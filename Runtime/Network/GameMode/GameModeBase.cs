using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[GameModeType(EGameModeType.Default)]
public abstract class GameModeBase
{
    protected GameStateBase _gameState;
    protected float _countdownDuration = 3f;

    public virtual void Initialize(GameStateBase gameState)
    {
        _gameState = gameState;
        _gameState.CurrentPhase.Value = EGamePhase.WaitingForPlayers;
    }

    public virtual void Tick()
    {
        if (!NetworkManager.Singleton.IsServer || _gameState == null) return;

        switch (_gameState.CurrentPhase.Value)
        {
            case EGamePhase.WaitingForPlayers:
                TickWaitingForPlayers();
                break;
            case EGamePhase.Countdown:
                TickCountdown();
                break;
            case EGamePhase.InProgress:
                TickInProgress();
                break;
            case EGamePhase.RoundOver:
                TickRoundOver();
                break;
        }
    }

    #region Tick
    protected abstract void TickWaitingForPlayers();
    protected virtual void TickCountdown()
    {
        _gameState.CountdownTimer.Value -= Time.deltaTime;

        if(_gameState.CountdownTimer.Value <= 0f)
        {
            _gameState.CurrentPhase.Value = EGamePhase.InProgress;
        }
    }
    protected abstract void TickInProgress();
    protected abstract void TickRoundOver();
    #endregion

    public virtual void OnAllPlayerSpawned()
    {
        InGameManager.Instance.AddLog($"GameMode - AllPlayerSpawned", ELogLevel.SystemInfo);
        _gameState.CurrentPhase.Value = EGamePhase.Countdown;
        _gameState.CountdownTimer.Value = _countdownDuration;
    }

    public virtual void OnAllPlayerDespawned()
    {
        InGameManager.Instance.AddLog($"GameMode - AllPlayerDespawned", ELogLevel.SystemInfo);
        _gameState.CurrentPhase.Value = EGamePhase.WaitingForPlayers;
    }


    #region Kill
    public abstract void KillPlayer(ulong vimtimId);
    public abstract void KillPlayer(ulong victimId, ulong killerId);
    #endregion

    #region Spawn/Despawn
    public void SpawnAllPlayers()
    {
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayer(clientId);
        }

        OnAllPlayerSpawned();
    }

    public void DespawnAllPlayers()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            DespawnPlayer(clientId);
        }

        OnAllPlayerDespawned();
    }

    protected void SpawnPlayer(ulong clientId)
    {
        //플레이어 스폰
        GameModeStruct currentGameModeStruct = InGameManager.Instance.GetGameModeStruct();

        if (currentGameModeStruct.PlayerPrefab == null)
        {
            Debug.LogError($"PlayerPrefab for GameMode '{currentGameModeStruct.GameModeType}' is not assigned in the ServerGameManager.");
            return;
        }
        GameObject playerPrefab = currentGameModeStruct.PlayerPrefab;

        Vector3 spawnPos = Vector3.zero;
        GameObject playerInstance = GameObject.Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        networkObject.SpawnAsPlayerObject(clientId);

        //플레이어 설정
        FixedString32Bytes playerName = $"Player {clientId}";
        foreach (var playerData in GameSessionSettings.Instance.PlayerDatasInGame)
        {
            if (playerData.ClientId == clientId)
            {
                playerName = playerData.PlayerName;
                break;
            }
        }
        if (playerInstance.TryGetComponent<SamplePlayerController>(out SamplePlayerController samplePlayerController))
        {
            samplePlayerController.PlayerName.Value = playerName;
        }
        InGameManager.Instance.AddLog($"GameMode - SpawnPlayer(Client: {clientId})", ELogLevel.SystemInfo);
    }

    protected void DespawnPlayer(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient))
        {
            if (networkClient.PlayerObject != null)
            {
                networkClient.PlayerObject.Despawn(true);
                InGameManager.Instance.AddLog($"GameMode - DespawnPlayer(Client: {clientId})", ELogLevel.SystemInfo);
                return;
            }
        }
        InGameManager.Instance.AddLog($"GameMode - Cant DespawnPlayer(Client: {clientId})", ELogLevel.SystemInfo);
    }
    #endregion

    public abstract void PlayerGetScore<T>(ulong clientId, T score);

}

[AttributeUsage(AttributeTargets.Class)]
public class GameModeTypeAttribute : Attribute
{
    public EGameModeType GameModeType { get; }

    public GameModeTypeAttribute(EGameModeType gameModeType)
    {
        GameModeType = gameModeType;
    }
}