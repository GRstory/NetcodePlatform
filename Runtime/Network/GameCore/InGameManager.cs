using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InGameManager : SingletonNetwork<InGameManager>
{
    public static event Action<List<InGameLog>> OnLogUpdated;
    public static event Action OnGameReset;

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

            _currentGameMode.SpawnAllPlayers();
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
        if (_gameModeTypeDict.TryGetValue(gameModeType, out Type value))
        {
            return (GameModeBase)Activator.CreateInstance(value, gameState);
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
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("Lobby");
    }

    public void RequestReplay()
    {
        if (!IsServer) return;

        if(_currentGameMode != null)
        {
            _currentGameMode.ResetGame();
        }
        OnGameReset?.Invoke();
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
