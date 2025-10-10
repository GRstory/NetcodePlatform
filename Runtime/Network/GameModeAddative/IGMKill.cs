using System;

public interface IGMKill<TReason> where TReason : Enum
{
    void KillPlayer(ulong victimId, TReason reason = default);
    void KillPlayer(ulong victimId, ulong killerId, TReason reason = default);
}