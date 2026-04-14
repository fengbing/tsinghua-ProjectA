using Gley.TrafficSystem.Internal;
using System.Collections;
using UnityEngine;

namespace Gley.TrafficSystem
{
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficParticipant : MonoBehaviour, ITrafficParticipant
    {
        private Rigidbody _rb;
        private bool _initialized;
        private Transform _myTransform;
#if GLEY_TRAFFIC_SYSTEM
        private void OnEnable()
        {
            StartCoroutine(Initialize());
        }

        IEnumerator Initialize()
        {
            while (!TrafficManager.Instance.Initialized)
            {
                yield return null;
            }
            _rb = GetComponent<Rigidbody>();
            _myTransform = transform;
            _initialized = true;
        }
#endif

        public bool AlreadyCollidingWith(Collider[] allColliders)
        {
            return false;
        }

        public float GetCurrentSpeedMS()
        {
            if (!_initialized)
            {
                return 0f;
            }
#if UNITY_6000_0_OR_NEWER
            return _rb.linearVelocity.magnitude;
#else
            return _rb.velocity.magnitude;
#endif
        }

        public Vector3 GetHeading()
        {
            return _myTransform.forward;
        }
    }
}
