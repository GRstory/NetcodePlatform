using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Kill Command", menuName = "Console Commands/Kill Command")]
public class ConsoleCommandKill : ConsoleCommandSO
{
    public override bool Process(string[] args)
    {
        if (args.Length == 1)
        {
            if (GameSessionSettings.Instance.TryGetClientIdByNickname(args[0], out ulong clientId))
            {
                InGameManager.Instance.CurrentGameMode.KillPlayer(clientId, EDeathReason.None);
                return true;
            }
            else if(ulong.TryParse(args[0],out ulong clientId2))
            {
                InGameManager.Instance.CurrentGameMode.KillPlayer(clientId2, EDeathReason.None);
                return true;
            }
        }
        else if (args.Length == 2)
        {
            bool victimFound = GameSessionSettings.Instance.TryGetClientIdByNickname(args[0], out ulong victimId);
            bool attackerFound = GameSessionSettings.Instance.TryGetClientIdByNickname(args[1], out ulong attackerId);

            if (victimFound && attackerFound)
            {
                InGameManager.Instance.CurrentGameMode.KillPlayer(victimId, attackerId, EDeathReason.None);
                return true;
            }
            else
            {
                if (!victimFound)
                {
                    victimFound =  ulong.TryParse(args[0], out victimId);
                }
                if (!attackerFound)
                {
                    attackerFound = ulong.TryParse(args[0], out attackerId);
                }
                if (victimFound && attackerFound)
                {
                    InGameManager.Instance.CurrentGameMode.KillPlayer(victimId, attackerId, EDeathReason.None);
                    return true;
                }
            }
        }

        return false;
    }

    public override string[] GetSamples()
    {
        return new string[]
        {
            "kill <target_name>",
            "kill <target_clientId>",
            "kill <target_name> <instigator_name>",
            "kill <target_clientId> <instigator_clientId>"
        };
    }
}
