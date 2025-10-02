using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class ConsoleBehaviour : MonoBehaviour
{
    [SerializeField] private TMP_InputField _consoleInput;
    [SerializeField] private RectTransform _suggestionPanel;
    [SerializeField] private TMP_Text _suggestionText;

    [SerializeField] private List<ConsoleCommandSO> _commandList = new List<ConsoleCommandSO>();

    private void Awake()
    {
        _consoleInput.onEndEdit.AddListener(ProcessInput);
        _consoleInput.onValueChanged.AddListener(UpdateSuggestions);
    }

    private void OnDestroy()
    {
        _consoleInput.onEndEdit.RemoveAllListeners();
        _consoleInput.onValueChanged.RemoveAllListeners();
    }

    private void ProcessInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        _suggestionPanel.gameObject.SetActive(false);

        string[] parts = input.Split(' ');
        if (parts.Length == 0 || !parts[0].StartsWith("/")) return;

        string commandWord = parts[0].Substring(1);
        string[] args = parts.Skip(1).ToArray();

        ConsoleCommandSO command = _commandList.FirstOrDefault(c => c.CommandWord.Equals(commandWord, System.StringComparison.OrdinalIgnoreCase));

        if (command != null)
        {
            if (command.Process(args))
            {
                InGameManager.Instance.AddLog($"Command Run:  <color=#ffffff>{input}</color>", ELogLevel.SystemInfo);
            }
            else
            {
                InGameManager.Instance.AddLog($"Command Fail: <color=#ff0000>{input}</color>", ELogLevel.SystemInfo);
            }
        }

        _consoleInput.text = "";
        _consoleInput.ActivateInputField();
    }

    private void UpdateSuggestions(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith("/"))
        {
            _suggestionText.gameObject.SetActive(false);
            return;
        }

        string commandWord = input.Substring(1).Split(' ')[0];
        var matchingCommands = _commandList.Where(c => c.CommandWord.StartsWith(commandWord, System.StringComparison.OrdinalIgnoreCase));

        if (!matchingCommands.Any())
        {
            _suggestionText.gameObject.SetActive(false);
            return;
        }

        var suggestionsBuilder = new System.Text.StringBuilder();

        foreach (var command in matchingCommands)
        {
            foreach (var sample in command.GetSamples())
            {
                suggestionsBuilder.AppendLine($"/{sample}");
            }
        }

        _suggestionText.gameObject.SetActive(true);
        _suggestionText.text = suggestionsBuilder.ToString();
    }
}
