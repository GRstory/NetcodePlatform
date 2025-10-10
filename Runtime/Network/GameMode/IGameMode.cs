using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public interface IGameMode
{
    public void Initialize(GameStateBase gameState);
    void Tick();
    void TickInProgress();
    void TickRoundOver();
    void TickWaitingForPlayers();
    void TickCountdown();

    void SpawnAllPlayers();
    void DespawnAllPlayers();
    void SpawnPlayer(ulong clientId);
    void DespawnPlayer(ulong clientId);

    void OnAllPlayerSpawned();
    void OnAllPlayerDespawned();
}
