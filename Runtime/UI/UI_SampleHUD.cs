using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UI_SampleHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text _gamePhaseText;
    [SerializeField] private TMP_Text _countdownText;

    [SerializeField] private GameObject _logPanel;
    [SerializeField] private TMP_Text _logText;
    [SerializeField] private Scrollbar _logScrollBar;

    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _replayButton;

    private Dictionary<ELogLevel, string> _logColorDict = new Dictionary<ELogLevel, string>
    {
        { ELogLevel.Info, "#FFFFFF" },
        { ELogLevel.Warn, "#FFFF00" },
        { ELogLevel.Error, "#FF0000" },
        { ELogLevel.SystemInfo, "#00FF00" } 
    };

    private void Awake()
    {
        //GameState의 OnNetworkSpawn()이 먼저 호출될 때 대비.
        if(GameStateBase.Instance != null)
        {
            HandleGameStateReady(GameStateBase.Instance);
        }
        else
        {
            GameStateBase.OnGameStateReady += HandleGameStateReady;
        }

        InGameManager.OnLogUpdated += HandleScrollLog;
        _exitButton.onClick.AddListener(OnExitButtonClick);
        _replayButton.onClick.AddListener(OnReplayButtonClick);

        _exitButton.gameObject.SetActive(false);
        _replayButton.gameObject.SetActive(false);
    }

    private void Start()
    {
        GameStateBase.Instance.CurrentPhase.OnValueChanged += HandleGameStatePhaseChanged;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.L))
        {
            _logPanel.SetActive(!_logPanel.activeSelf);
        }
    }

    public void OnDestroy()
    {
        GameStateBase.OnGameStateReady -= HandleGameStateReady;
        GameStateBase.Instance.CurrentPhase.OnValueChanged -= HandleGameStatePhaseChanged;
        InGameManager.OnLogUpdated -= HandleScrollLog;

        _exitButton.onClick.RemoveAllListeners();
        _replayButton.onClick.RemoveAllListeners();
    }

    private void HandleGameStateReady(GameStateBase gameState)
    {
        gameState.CurrentPhase.OnValueChanged += HandleGamePhaseChanged;
        gameState.CountdownTimer.OnValueChanged += HandleGameCountdownChanged;

        HandleGamePhaseChanged(default, gameState.CurrentPhase.Value);
        HandleGameCountdownChanged(default, gameState.CountdownTimer.Value);
    }

    private void HandleGamePhaseChanged(EGamePhase previousValue, EGamePhase newValue)
    {
        _gamePhaseText.text = $"Phase: {newValue}";
    }

    private void HandleGameCountdownChanged(float previousValue, float newValue)
    {
        if (newValue > 0)
        {
            _countdownText.gameObject.SetActive(true);
            _countdownText.text = newValue.ToString("F0");
        }
        else
        {
            _countdownText.gameObject.SetActive(false);
        }
    }

    private void HandleGameStatePhaseChanged(EGamePhase oldPhase, EGamePhase newPhase)
    {
        switch (newPhase)
        {
            case EGamePhase.WaitingForPlayers:
                _exitButton.gameObject.SetActive(false);
                _replayButton.gameObject.SetActive(false);
                break;
            case EGamePhase.Countdown:
                break;
            case EGamePhase.InProgress:
                break;
            case EGamePhase.RoundOver:
                _exitButton.gameObject.SetActive(true);
                if(NetworkManager.Singleton.IsHost)
                {
                    _replayButton.gameObject.SetActive(true);
                }
                break;
        }
    }

    private void HandleScrollLog(List<InGameLog> logListRef)
    {
        int numLine = Math.Min(logListRef.Count, 30);
        int beginLog = logListRef.Count - numLine;
        beginLog = (int)(beginLog * (1 - _logScrollBar.value));

        beginLog = Math.Max(0, Math.Min(beginLog, logListRef.Count - numLine));

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < numLine; ++i)
        {
            int currentIndex = beginLog + i;
            if (currentIndex >= logListRef.Count) break;

            InGameLog currentLog = logListRef[currentIndex];
            if (!_logColorDict.TryGetValue(currentLog.LogLevel, out string colorHex))
            {
                colorHex = "#FFFFFF"; // 기본값
            }

            string logLine = $"[{currentLog.LogLevel}] {currentLog.LogText}";
            sb.AppendLine($"<color={colorHex}>{logLine}</color>");
        }

        _logText.text = sb.ToString();
    }

    #region UI 

    private void OnExitButtonClick()
    {
        InGameManager.Instance.ExitGame();
    }

    private void OnReplayButtonClick()
    {
        InGameManager.Instance.RequestReplay();
    }

    #endregion
}
