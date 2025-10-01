using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class SamplePlayerController : NetworkBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private bool _canMove = false;
    [SerializeField] private bool _isStunned = false;
    [SerializeField] private TMP_Text _playerNameText;

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    private void Update()
    {
        if (!IsOwner || !_canMove) return;

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 moveDirection = (transform.forward * verticalInput) + (transform.right * horizontalInput);
        if (moveDirection.sqrMagnitude > 1)
        {
            moveDirection.Normalize();
        }
        transform.position += moveDirection * _moveSpeed * Time.deltaTime;
    }

    public override void OnNetworkSpawn()
    {
        PlayerName.OnValueChanged += OnNameChanged;
        GameStateBase.Instance.CurrentPhase.OnValueChanged += OnGamePhaseChanged;
        OnGamePhaseChanged(EGamePhase.WaitingForPlayers, GameStateBase.Instance.CurrentPhase.Value);
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= OnNameChanged;
        GameStateBase.Instance.CurrentPhase.OnValueChanged -= OnGamePhaseChanged;
    }

    private void OnNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        InGameManager.Instance.AddLog($"PlayerController - OnNameChanged{newValue.ToString()}");
        _playerNameText.text = newValue.ToString();
    }

    private void OnGamePhaseChanged(EGamePhase previousPhase, EGamePhase newPhase)
    {
        _canMove = (newPhase == EGamePhase.InProgress) && !_isStunned;
    }

}
