using UnityEngine;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float smoothTime = 0.16f;

        private Transform _target;
        private Vector3 _offset;
        private Vector3 _velocity;

        public void Bind(Transform target)
        {
            _target = target;
            _offset = target != null
                ? transform.position - target.position
                : new Vector3(0f, 0f, -10f);
        }

        public void SnapToTarget()
        {
            if (_target == null)
                return;

            _velocity = Vector3.zero;
            transform.position = _target.position + _offset;
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;

            Vector3 desiredPosition = _target.position + _offset;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _velocity,
                smoothTime);
        }
    }
}
