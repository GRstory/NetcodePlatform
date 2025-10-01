using System;
using Unity.Netcode;


public abstract class GameStateBase : SingletonNetwork<GameStateBase>
{
    public static event Action<GameStateBase> OnGameStateReady;
    public NetworkVariable<EGamePhase> CurrentPhase = new NetworkVariable<EGamePhase>();
    public NetworkVariable<float> GameTimer = new NetworkVariable<float>();
    public NetworkVariable<float> CountdownTimer = new NetworkVariable<float>();

    public override void OnNetworkSpawn()
    {
        OnGameStateReady?.Invoke(this);
        CurrentPhase.OnValueChanged += CurrentPhaseOnValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        CurrentPhase.OnValueChanged -= CurrentPhaseOnValueChanged;
    }

    [ServerRpc]
    public void RequestPhaseChangeServerRpc(EGamePhase phase)
    {
        CurrentPhase.Value = phase;
    }

    protected virtual void CurrentPhaseOnValueChanged(EGamePhase oldPhase, EGamePhase newPhase)
    {
        InGameManager.Instance.AddLog($"GameState - Phase Changed: OLD: {oldPhase} | NEW: {newPhase}", ELogLevel.SystemInfo);
    }
}
