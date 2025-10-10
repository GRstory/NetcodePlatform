using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[GameModeType(EGameModeType.Sample)]
public class SampleGameMode : GameModeBase<SampleGameState>, IGMProgress<float>, IGMKill<EDeathReason>
{
    private bool _isLoggedPlayerData = false;
    private float _runningDuration = 30f;
    private float _currentRunningTime = 0f;

    public override void TickInProgress()
    {
        _currentRunningTime += Time.deltaTime;
        if(_runningDuration < _currentRunningTime)
        {
            _gameState.CurrentPhase.Value = EGamePhase.RoundOver;
        }
    }

    public override void TickRoundOver()
    {
        
    }

    public override void TickWaitingForPlayers()
    {
        
    }

    public override void TickCountdown()
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


    public void KillPlayer(ulong victimId, EDeathReason reason)
    {
        InGameManager.Instance.AddLog($"Gamemode - KillPlayer: Client{victimId}");
    }

    public void KillPlayer(ulong victimId, ulong killerId, EDeathReason reason)
    {
        InGameManager.Instance.AddLog($"Gamemode - KillPlayer: Client{killerId} kill Client{victimId}");
    }

    public void PlayerGetScore(ulong clientId, float score)
    {
        
    }
}
