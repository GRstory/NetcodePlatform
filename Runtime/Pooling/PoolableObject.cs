using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolableObject : MonoBehaviour
{
    public string AddressableKey { get; set; }
    public bool IsPooled { get; set; }

    public virtual void OnSpawn()
    {

    }

    public virtual void OnDespawn()
    {

    }
}
