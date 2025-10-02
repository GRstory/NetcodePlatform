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


    public override void KillPlayer(ulong victimId)
    {
        InGameManager.Instance.AddLog($"Gamemode - KillPlayer: Client{victimId}");
    }

    public override void KillPlayer(ulong victimId, ulong killerId)
    {
        InGameManager.Instance.AddLog($"Gamemode - KillPlayer: Client{killerId} kill Client{victimId}");
    }
}
