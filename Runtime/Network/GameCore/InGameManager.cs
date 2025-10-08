using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InGameManager : SingletonNetwork<InGameManager>
{
    public static event Action<List<InGameLog>> OnLogUpdated;
    public IGameMode CurrentGameMode { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private List<GameModeStruct> _gameModeStructList = new List<GameModeStruct>();

    private Dictionary<EGameModeType, Type> _gameModeTypeDict = new Dictionary<EGameModeType, Type>();
    private int _clientsLoadedSceneCount = 0;
    //Log
    private List<InGameLog> _logList = new List<InGameLog>();

    private void Update()
    {
        if (!IsServer || CurrentGameMode == null) return;

        CurrentGameMode.Tick();
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
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
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

            CurrentGameMode.SpawnAllPlayers();
        }
    }

    private void InitializeGameMode()
    {
        //게임모드클래스 딕셔너리 설정
        var gameModeClassList = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsSubclassOf(typeof(IGameMode)) && !t.IsAbstract);
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

            CurrentGameMode = GetGameMode(gameModeType, gameStateInstance);
            CurrentGameMode.Initialize(gameStateInstance);
            AddLog($"InGameManager - Current GameMode: {CurrentGameMode}", ELogLevel.SystemInfo);
            AddLog($"InGameManager - Create GameState", ELogLevel.SystemInfo);
        }
    }

    private IGameMode GetGameMode(EGameModeType gameModeType, GameStateBase gameState)
    {
        if (_gameModeTypeDict.TryGetValue(gameModeType, out Type value))
        {
            return (IGameMode)Activator.CreateInstance(value, gameState);
        }
        return null;
    }

    public GameModeStruct GetGameModeStruct()
    {
        EGameModeType currentGameModeType = GameSessionSettings.Instance.SelectedGameMode.Value;
        return _gameModeStructList.FirstOrDefault(x => x.GameModeType == currentGameModeType);
    }

    public void ExitGame()
    {
        if(IsServer)
        {
            CurrentGameMode.DespawnAllPlayers();
            GameSessionSettings.Instance.IsGameStarted.Value = false;
            GameStateBase.Instance.gameObject.GetComponent<NetworkObject>().Despawn();
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else if(IsClient)
        {
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene("Lobby");
        }
    }

    public void RequestReplay()
    {
        if (!IsServer) return;

        if(CurrentGameMode != null)
        {
            CurrentGameMode.DespawnAllPlayers();
        }
        //다른 네트워크 오브젝트 싹다 삭제

        //게임스테이트 삭제
        GameStateBase.Instance.GetComponent<NetworkObject>().Despawn();

        //씬 재로딩
        NetworkManager.Singleton.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if(!IsServer) return;

        for (int i = 0; i < GameSessionSettings.Instance.PlayerDatasInGame.Count; i++)
        {
            if (GameSessionSettings.Instance.PlayerDatasInGame[i].ClientId == clientId)
            {
                GameSessionSettings.Instance.PlayerDatasInGame.RemoveAt(i);
                AddLog($"Player removed: {clientId}", ELogLevel.SystemInfo);
                break;
            }
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
