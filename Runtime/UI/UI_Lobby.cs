using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UI_Lobby : MonoBehaviour
{
    [Header("Main Panel")]
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private TMP_InputField _joinCodeField;

    [Header("Select Panel")]
    [SerializeField] private Button _startButton;
    [SerializeField] private GameObject _selectGameModePanel;
    [SerializeField] private TMP_Dropdown _gameModeDropdown;

    [Header("Info Panel")]
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private Button _changeNameButton;
    [SerializeField] private GameObject _connectedPlayerListObject;
    [SerializeField] private GameObject _connectedPlayerPanelPrefab;

    private List<UI_ConnectedPlayer> _connectedPlayerList = new List<UI_ConnectedPlayer>();

    private void Start()
    {
        LobbyManager.Instance.OnLobbyStateChanged += HandleLobbyStateChanged;
        LobbyManager.Instance.PlayerDataList.OnListChanged += HandlePlayerListChanged;

        _hostButton.onClick.AddListener(() => LobbyManager.Instance.CreateLobby());
        _joinButton.onClick.AddListener(() => LobbyManager.Instance.JoinLobby(_joinCodeField.text));
        _startButton.onClick.AddListener(() => LobbyManager.Instance.StartGame());
        _changeNameButton.onClick.AddListener(HandleSetPlayerNameChanged);
        _gameModeDropdown.onValueChanged.AddListener(HandleGameModeDropdownValueChanged);
        _selectGameModePanel.SetActive(false);

        _gameModeDropdown.ClearOptions();
        List<string> modeList = new List<string>(Enum.GetNames(typeof(EGameModeType)));
        _gameModeDropdown.AddOptions(modeList);

        for(int i = 0; i < LobbyManager.Instance.MaxPlayer; i++)
        {
            GameObject newUI = Instantiate(_connectedPlayerPanelPrefab, _connectedPlayerListObject.transform);
            UI_ConnectedPlayer connectedPlayer = newUI.GetComponent<UI_ConnectedPlayer>();
            connectedPlayer.UpdateDisplay(new PlayerData());
            _connectedPlayerList.Add(connectedPlayer);
        }
    }

    private void HandleLobbyStateChanged(ELobbyState state, string reason)
    {
        _statusText.text = reason;

        switch (state)
        {
            case ELobbyState.Idle:
                _hostButton.interactable = true;
                _joinButton.interactable = true;
                _selectGameModePanel.SetActive(false);
                for (int i = 0; i < _connectedPlayerList.Count; i++)
                {
                    _connectedPlayerList[i].UpdateDisplay(new PlayerData());
                }
                break;
            case ELobbyState.Connecting:
                _hostButton.interactable = false;
                _joinButton.interactable = false;
                _selectGameModePanel.SetActive(false);
                break;
            case ELobbyState.HostSuccess:
                _hostButton.interactable = false;
                _joinButton.interactable = false;
                _selectGameModePanel.SetActive(true);
                break;
            case ELobbyState.ClientSuccess:
                _hostButton.interactable = false;
                _joinButton.interactable = false;
                _selectGameModePanel.SetActive(false);
                break;
            case ELobbyState.Error:
                _hostButton.interactable = true;
                _joinButton.interactable = true;
                _selectGameModePanel.SetActive(false);
                break;
        }
    }

    private void HandleGameModeDropdownValueChanged(int index)
    {
        EGameModeType type = (EGameModeType)index;
        GameSessionSettings.Instance.SelectedGameMode.Value = type;
    }

    private void HandlePlayerListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        int cnt = Math.Min(_connectedPlayerList.Count, LobbyManager.Instance.PlayerDataList.Count);
        for (int i = 0; i < cnt; i++)
        {
            _connectedPlayerList[i].UpdateDisplay(LobbyManager.Instance.PlayerDataList[i]);
            //Debug.Log($"{i}: {LobbyManager.Instance.PlayerDataList[i].PlayerName}");
        }
        for(int i = cnt; i < _connectedPlayerList.Count; i++)
        {
            _connectedPlayerList[i].UpdateDisplay(new PlayerData());
            //Debug.Log($"{i}: null");
        }
    }

    private void HandleSetPlayerNameChanged()
    {
        string nickname = _nameField.text;
        if (!string.IsNullOrWhiteSpace(nickname))
        {
            LobbyManager.Instance.SetPlayerName(nickname);
        }
    }
}
