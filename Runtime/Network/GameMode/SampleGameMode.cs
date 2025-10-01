using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[GameModeType(EGameModeType.Sample)]
public class SampleGameMode : GameModeBase
{
    private bool _isLoggedPlayerData = false;
    private float _runningDuration = 30f;
    private float _currentRunningTime = 0f;

    public override void OnPlayerKilled(ulong playerId)
    {
        
    }

    public override void RequestRespawn()
    {
        
    }

    protected override void TickInProgress()
    {
        _currentRunningTime += Time.deltaTime;
        if(_runningDuration < _currentRunningTime)
        {
            _gameState.CurrentPhase.Value = EGamePhase.RoundOver;
        }
    }

    protected override void TickRoundOver()
    {
        
    }

    protected override void TickWaitingForPlayers()
    {
        
    }

    protected override void TickCountdown()
    {
        base.TickCountdown();
        if(!_isLoggedPlayerData)
        {
            foreach(var data in GameSessionSettings.Instance.PlayerDatasInGame)
            {
                Debug.Log($"Client {data.ClientId}: {data.PlayerName} connected");
            }
            _isLoggedPlayerData = true;
        }
    }

    public override void ResetGame()
    {

    }

    protected override void SpawnPlayer(ulong clientId)
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
    }
}
