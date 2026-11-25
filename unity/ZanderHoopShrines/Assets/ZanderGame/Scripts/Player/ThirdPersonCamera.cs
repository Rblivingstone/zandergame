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
        float _stickIdle;
        bool _recentering;

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
            float dt = Time.deltaTime;
            if (canLook)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    var mouse = Controls.MouseLook;
                    Yaw += mouse.x * Sensitivity;
                    Pitch -= mouse.y * Sensitivity;
                    Distance = Mathf.Clamp(Distance - Controls.Zoom * 8f, MinDistance, MaxDistance);
                }
                // controller: right stick (with a gentle response curve for fine aiming)
                var stick = Controls.StickLook;
                var curved = stick * stick.magnitude;
                Yaw += curved.x * Controls.LookSpeed * dt;
                Pitch -= curved.y * Controls.LookSpeed * 0.65f * dt;
                if (stick.sqrMagnitude > 0.01f) _stickIdle = 0f;
                else _stickIdle += dt;

                var player = PlayerController.Instance;
                if (Controls.RecenterDown && player != null) _recentering = true;
                if (_recentering && player != null)
                {
                    float target = Quaternion.LookRotation(player.Forward).eulerAngles.y;
                    Yaw = Mathf.MoveTowardsAngle(Yaw, target, 540f * dt);
                    Pitch = Mathf.MoveTowards(Pitch, 18f, 120f * dt);
                    if (Mathf.Abs(Mathf.DeltaAngle(Yaw, target)) < 0.5f) _recentering = false;
                }
                else if (Controls.UsingGamepad && _stickIdle > 1.0f && player != null && player.HorizontalSpeed > 2f &&
                         !(player.Balls != null && player.Balls.Charging))
                {
                    // auto-follow: drift behind Zander while he runs (keeps little players oriented)
                    float target = Quaternion.LookRotation(player.Forward).eulerAngles.y;
                    float delta = Mathf.DeltaAngle(Yaw, target);
                    if (Mathf.Abs(delta) < 150f) Yaw += delta * Mathf.Clamp01(1.2f * dt);
                }
                Pitch = Mathf.Clamp(Pitch, -25f, 70f);
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
