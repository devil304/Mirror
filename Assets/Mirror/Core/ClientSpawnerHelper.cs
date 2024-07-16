using System.Collections;
using System.Collections.Generic;
using SharedCode;
using UnityEngine;
using Zenject;

namespace Mirror
{
    public class ClientSpawnerHelper : MonoBehaviour
    {
        [Inject] private InjectablePrefab.Factory _factory;
        public bool Ready => _factory != null;

        private void Start()
        {
            NetworkClient.RegisterSpawnerHelper(this);
        }

        public GameObject Create(Object prefab)
        {
            return _factory.Create(prefab).gameObject;
        }
    }
}
