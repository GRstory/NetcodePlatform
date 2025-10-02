using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManagerSingletonHelper : SingletonBehavior<NetworkManagerSingletonHelper>
{
    protected override void Awake()
    {
        base.Awake();

        DontDestroyOnLoad(this.gameObject);
    }
}
