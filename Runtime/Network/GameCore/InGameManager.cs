using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class InGameManager : SingletonNetwork<InGameManager>
{
    public static event Action<List<InGameLog>> OnLogUpdated;

    [Header("Game Settings")]
    [SerializeField] private List<GameModeStruct> _gameModeStructList = new List<GameModeStruct>();

    private Dictionary<EGameModeType, Type> _gameModeTypeDict = new Dictionary<EGameModeType, Type>();
    private GameModeBase _currentGameMode;
    private int _clientsLoadedSceneCount = 0;
    //Log
    private List<InGameLog> _logList = new List<InGameLog>();

    private void Update()
    {
        if (!IsServer || _currentGameMode == null) return;

        _currentGameMode.Tick();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        AddLog("InGameManager - OnNetworkSpawn", ELogLevel.SystemInfo);
        InitializeGameMode();
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
    }

    private void OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        foreach (var clientId in clientsCompleted)
        {
            _clientsLoadedSceneCount++;
        }

        if (_clientsLoadedSceneCount == NetworkManager.Singleton.ConnectedClientsIds.Count)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;

            SpawnAllPlayers();
        }
    }

    private void InitializeGameMode()
    {
        //게임모드클래스 딕셔너리 설정
        var gameModeClassList = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsSubclassOf(typeof(GameModeBase)) && !t.IsAbstract);
        foreach (var gameModeClass in gameModeClassList)
        {
            var attribute = gameModeClass.GetCustomAttribute<GameModeTypeAttribute>();
            if (attribute != null)
            {
                _gameModeTypeDict.Add(attribute.GameModeType, gameModeClass);
            }
        }

        //게임세팅 확인
        if (GameSessionSettings.Instance == null)
        {
            AddLog($"InGameManager - GameSessionSettings is Null", ELogLevel.Warn);
            return;
        }

        //게임모드 설정 및 게임스테이트 생성
        EGameModeType gameModeType = GameSessionSettings.Instance.SelectedGameMode.Value;
        GameObject gameStatePrefab = _gameModeStructList.FirstOrDefault(x => x.GameModeType == gameModeType).GameStatePrefab;
        if (gameStatePrefab != null)
        {
            GameStateBase gameStateInstance = Instantiate(gameStatePrefab).GetComponent<GameStateBase>();
            gameStateInstance.GetComponent<NetworkObject>().Spawn();

            _currentGameMode = GetGameMode(gameModeType, gameStateInstance);
            _currentGameMode.Initialize(gameStateInstance);
            AddLog($"InGameManager - Current GameMode: {_currentGameMode}", ELogLevel.SystemInfo);
            AddLog($"InGameManager - Create GameState", ELogLevel.SystemInfo);
        }
    }

    private GameModeBase GetGameMode(EGameModeType gameModeType, GameStateBase gameState)
    {
        if(_gameModeTypeDict.TryGetValue(gameModeType, out Type value))
        {
            return (GameModeBase)Activator.CreateInstance(value, gameState);
        }
        return null;
    }

    private void SpawnAllPlayers()
    {
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            AddLog($"InGameManager - SpawnPlayer(Client: {clientId})", ELogLevel.SystemInfo);
            SpawnPlayer(clientId);
        }

        if(_currentGameMode != null)
        {
            _currentGameMode.OnAllPlayerSpawned();
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        //플레이어 스폰
        EGameModeType currentGameModeType = GameSessionSettings.Instance.SelectedGameMode.Value;
        GameModeStruct currentGameModeStruct = _gameModeStructList.FirstOrDefault(x => x.GameModeType == currentGameModeType);

        if (currentGameModeStruct.PlayerPrefab == null)
        {
            Debug.LogError($"PlayerPrefab for GameMode '{currentGameModeType}' is not assigned in the ServerGameManager.");
            return;
        }
        GameObject playerPrefab = currentGameModeStruct.PlayerPrefab;

        Vector3 spawnPos = Vector3.zero;
        GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
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
        if(playerInstance.TryGetComponent<SamplePlayerController>(out SamplePlayerController samplePlayerController))
        {
            samplePlayerController.PlayerName.Value = playerName;
        }
    }

    #region Log System
    public void AddLog(string log, ELogLevel logLevel = ELogLevel.Info)
    {
        _logList.Add(new InGameLog(log, logLevel));
        OnLogUpdated?.Invoke(_logList);
    }

    public void SaveLog()
    {
        string fileName = DateTime.Now.ToString("yyMMddHHmmss");
        var logTexts = _logList.Select(log => $"[{log.LogLevel}] {log.LogText}");
        File.WriteAllLines($"Log/Log_{fileName}.txt", logTexts);
        _logList.Clear();
    }

    #endregion
}


[Serializable]
public struct GameModeStruct
{
    public EGameModeType GameModeType;
    public GameObject GameStatePrefab;
    public GameObject PlayerPrefab;
}
