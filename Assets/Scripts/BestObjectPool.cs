using UnityEngine;
using UnityEngine.Pool;

public class BestObjectPool<T> where T : MonoBehaviour
{
        private ObjectPool<T> _pool;
        
        public BestObjectPool(T prefab, int defaultPoolSize = 10, int maxSize = 100)
        {
            _pool = new ObjectPool<T>(
                createFunc: () =>
                {
                    var newObject = Object.Instantiate(prefab);
                    newObject.gameObject.SetActive(false);
                    return newObject;
                },
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false),
                actionOnDestroy: Object.Destroy,
                defaultCapacity: defaultPoolSize,
                maxSize: maxSize
            );

            // Prepopulate the pool
            var poolObjects = new T[defaultPoolSize];
            for (var i = 0; i < defaultPoolSize; i++)
            {
                poolObjects[i] = _pool.Get();
            }
            for (var i = 0; i < defaultPoolSize; i++)
            {
                _pool.Release(poolObjects[i]);
            }
        }
        
        public T GetObject() => _pool.Get();
        public void ReleaseObject(T obj) => _pool.Release(obj);
}