using UnityEngine;

namespace ZanderGame
{
    /// <summary>Mouse-orbit third-person camera that avoids clipping into walls.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 6f;
        public float MinDistance = 3f, MaxDistance = 10f;
        public float Sensitivity = 3f;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 18f;

        float _shake;
        float _currentDist;
        Vector3 _smoothedPivot;

        public void SnapBehind(float yaw)
        {
            Yaw = yaw;
            Pitch = 18f;
            if (Target != null) _smoothedPivot = Target.position + Vector3.up * 1.6f;
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        void LateUpdate()
        {
            if (Target == null) return;
            var gm = GameManager.Instance;
            bool canLook = gm == null || gm.IsPlaying;
            if (canLook && Cursor.lockState == CursorLockMode.Locked)
            {
                var look = Controls.Look;
                Yaw += look.x * Sensitivity;
                Pitch = Mathf.Clamp(Pitch - look.y * Sensitivity, -25f, 70f);
                Distance = Mathf.Clamp(Distance - Controls.Zoom * 8f, MinDistance, MaxDistance);
            }

            var pivotTarget = Target.position + Vector3.up * 1.6f;
            _smoothedPivot = _smoothedPivot == Vector3.zero
                ? pivotTarget
                : Vector3.Lerp(_smoothedPivot, pivotTarget, 1f - Mathf.Exp(-14f * Time.deltaTime));
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            var back = rot * Vector3.back;
            float want = Distance;
            if (Physics.SphereCast(_smoothedPivot, 0.25f, back, out var hit, Distance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                want = Mathf.Max(0.6f, hit.distance - 0.1f);
            _currentDist = want < _currentDist ? want : Mathf.Lerp(_currentDist, want, 1f - Mathf.Exp(-5f * Time.deltaTime));

            var pos = _smoothedPivot + back * _currentDist;
            if (_shake > 0f)
            {
                pos += Random.insideUnitSphere * _shake * 0.25f;
                _shake = Mathf.MoveTowards(_shake, 0, Time.deltaTime * 1.5f);
            }
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
