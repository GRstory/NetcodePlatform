using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SingletonNetwork<T> : NetworkBehaviour where T : NetworkBehaviour
{
    private static T _instance;
    public static T Instance { get { return _instance; } }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
