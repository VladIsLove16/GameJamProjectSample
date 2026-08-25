using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace JamStarter
{
    /// <summary>
    /// A non-global component pool with predictable activation and parenting semantics.
    /// The pool is intended to be owned and disposed by the system that uses it.
    /// </summary>
    public sealed class ComponentPool<T> : IDisposable where T : Component
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly Transform _inactiveParent;
        private readonly ObjectPool<T> _pool;

        public ComponentPool(
            Func<T> create,
            Transform inactiveParent = null,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            Action<T> onDestroy = null,
            bool collectionCheck = true,
            int defaultCapacity = 10,
            int maxSize = 10000)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _inactiveParent = inactiveParent;
            _onGet = onGet;
            _onRelease = onRelease;
            _onDestroy = onDestroy;

            _pool = new ObjectPool<T>(
                CreateInstance,
                ActivateInstance,
                DeactivateInstance,
                DestroyInstance,
                collectionCheck,
                defaultCapacity,
                maxSize);
        }

        public int CountAll => _pool.CountAll;

        public int CountActive => _pool.CountActive;

        public int CountInactive => _pool.CountInactive;

        public T Get()
        {
            return _pool.Get();
        }

        public PooledObject<T> Get(out T instance)
        {
            return _pool.Get(out instance);
        }

        public void Release(T instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            _pool.Release(instance);
        }

        public void Clear()
        {
            _pool.Clear();
        }

        public void Dispose()
        {
            _pool.Clear();
        }

        private T CreateInstance()
        {
            var instance = _create();
            if (instance == null)
            {
                throw new InvalidOperationException("The component pool factory returned null.");
            }

            instance.gameObject.SetActive(false);
            if (_inactiveParent != null)
            {
                instance.transform.SetParent(_inactiveParent, false);
            }

            return instance;
        }

        private void ActivateInstance(T instance)
        {
            instance.gameObject.SetActive(true);
            _onGet?.Invoke(instance);
        }

        private void DeactivateInstance(T instance)
        {
            _onRelease?.Invoke(instance);
            instance.gameObject.SetActive(false);

            if (_inactiveParent != null)
            {
                instance.transform.SetParent(_inactiveParent, false);
            }
        }

        private void DestroyInstance(T instance)
        {
            if (instance == null)
            {
                return;
            }

            _onDestroy?.Invoke(instance);
            Object.Destroy(instance.gameObject);
        }
    }
}
