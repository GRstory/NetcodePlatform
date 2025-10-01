using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UI_ConnectedPlayer : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button _kickButton;
    private ulong _clientId;

    private void Start()
    {
        _kickButton.onClick.AddListener(Kick);
    }

    private void Kick()
    {
        if(NetworkManager.Singleton.IsHost) LobbyManager.Instance.KickPlayer(_clientId);
        else LobbyManager.Instance.LeaveLobby();
    }

    public void UpdateDisplay(PlayerData playerData)
    {
        _clientId = playerData.ClientId;
        _nameText.text = playerData.PlayerName.ToString();

        bool isHost = NetworkManager.Singleton.IsHost;
        bool isSelf = _clientId == NetworkManager.Singleton.LocalClientId;

        _kickButton.interactable = ((isHost || isSelf) && !string.IsNullOrEmpty(playerData.PlayerName.ToString()));
    }
}
