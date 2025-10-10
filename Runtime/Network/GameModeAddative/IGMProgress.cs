using System;
using UnityEngine;

public interface IGMProgress<TScore> where TScore : struct
{
    void PlayerGetScore(ulong clientId, TScore score);
}
