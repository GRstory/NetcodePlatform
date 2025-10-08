using System.Collections;
using UnityEngine;

public class SamplePooledObject : PoolableObject
{
    [SerializeField] private float lifeTime = 5.0f;

    private Coroutine _returnCoroutine;

    public override void OnSpawn()
    {
        _returnCoroutine = StartCoroutine(ReturnToPoolAfterTime());
    }

    public override void OnDespawn()
    {
        if(_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }
    }

    private IEnumerator ReturnToPoolAfterTime()
    {
        yield return new WaitForSeconds(lifeTime);
        _returnCoroutine = null;

        PoolingManager.Instance.Return(this.gameObject);
    }
}
