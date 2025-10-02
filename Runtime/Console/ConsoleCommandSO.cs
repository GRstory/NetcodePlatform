using UnityEngine;

public abstract class ConsoleCommandSO : ScriptableObject
{
    [SerializeField] public string CommandWord = string.Empty;
    
    public abstract bool Process(string[] args);
    public virtual string[] GetSamples()
    {
        return new string[] { CommandWord };
    }
}
