using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleGameState : GameStateBase
{
    protected override void CurrentPhaseOnValueChanged(EGamePhase oldPhase, EGamePhase newPhase)
    {
        base.CurrentPhaseOnValueChanged(oldPhase, newPhase);

        switch(newPhase)
        {
            case EGamePhase.WaitingForPlayers:
                break;
            case EGamePhase.Countdown:
                break;
            case EGamePhase.InProgress:
                break;
            case EGamePhase.RoundOver:
                break;
        }
    }
}
