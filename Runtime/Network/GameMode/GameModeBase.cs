using System;
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

    public virtual void OnAllPlayerSpawned()
    {
        InGameManager.Instance.AddLog($"GameMode - AllPlayerSpawed", ELogLevel.SystemInfo);
        _gameState.CurrentPhase.Value = EGamePhase.Countdown;
        _gameState.CountdownTimer.Value = _countdownDuration;
    }

    public abstract void OnPlayerKilled(ulong playerId);
    public abstract void RequestRespawn();
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