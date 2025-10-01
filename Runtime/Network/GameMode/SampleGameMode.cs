using System;
using System.Collections;
using System.Collections.Generic;
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
}
