using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Drives Zander's animations. Uses the clips baked into Zander.fbx when they
    /// exist; otherwise animates the primitive fallback body procedurally.
    /// </summary>
    public class PlayerAnimation : MonoBehaviour
    {
        PlayerController _pc;
        ClipAnimator _clips;
        CharacterRig _rig;     // procedural fallback
        float _t;
        string _oneShot;
        float _oneShotUntil;

        public void Init(PlayerController pc)
        {
            _pc = pc;
            _clips = ClipAnimator.TryCreate(pc.Visual.gameObject, "Models/Zander");
            _rig = pc.Visual.GetComponent<CharacterRig>();
        }

        int _dunkPhase;   // 0 none, 1 leap, 2 slam, 3 hanging on the rim

        public void SetDunkPhase(int phase)
        {
            if (phase == _dunkPhase) return;
            _dunkPhase = phase;
            if (phase == 2)
            {
                _clips?.PlayOnce("Dunk", 1.5f);
                _oneShot = "Dunk";
                _oneShotUntil = Time.time + 0.4f;
            }
        }

        public void OnJump() => Trigger("Jump");
        public void OnHit() => Trigger("Hit");

        public void Trigger(string name)
        {
            if (name == "Jump")
            {
                if (_clips != null && _clips.Has("Jump") && !_clips.PlayingOneShot) _clips.SetBase("Jump", 1f, 0.05f);
                return;
            }
            _clips?.PlayOnce(name);
            _oneShot = name;
            _oneShotUntil = Time.time + (name == "Celebrate" ? 2.0f : name == "Steal" ? 0.5f : 0.6f);
        }

        int _facingChecks;

        void LateUpdate()
        {
            // a few frames after the animation starts, make sure the model faces forward
            if (_pc == null || _facingChecks >= 3) return;
            if (Time.frameCount % 5 != 0) return;
            _facingChecks++;
            if (ModelLibrary.FaceByFeet(_pc.Visual.gameObject, "Shoe")) _facingChecks = 3;
        }

        void Update()
        {
            if (_pc == null) return;
            float dt = Time.deltaTime;
            _t += dt;
            bool ball = _pc.Balls != null && _pc.Balls.HasBall;
            float spd = _pc.HorizontalSpeed;

            if (_clips != null && _dunkPhase > 0)
            {
                _clips.SetBase(_dunkPhase == 1 ? "Jump" : "RimHang", 1f, 0.12f);
                _clips.Tick(dt);
                return;
            }
            if (_clips != null)
            {
                if (_pc.Grounded || _pc.IsHanging)
                {
                    if (spd > 0.4f) _clips.SetBase(ball ? "DribbleRun" : "Run", Mathf.Clamp(spd / 6.5f, 0.6f, 1.5f));
                    else _clips.SetBase(ball ? "Dribble" : "Idle");
                }
                else if (_clips.CurrentBase != "jump") _clips.SetBase("Jump", 1f, 0.1f);
                _clips.Tick(dt);
                return;
            }
            if (_rig != null) Procedural(spd, ball);
        }

        void Procedural(float spd, bool ball)
        {
            float run = Mathf.Clamp01(spd / 6f);
            float ph = _t * (6f + 6f * run);
            float s = Mathf.Sin(ph);
            bool shooting = Time.time < _oneShotUntil && (_oneShot == "Shoot" || _oneShot == "Dunk");
            bool cheering = Time.time < _oneShotUntil && _oneShot == "Celebrate";
            bool charging = (_pc.Balls != null && _pc.Balls.Charging) || _dunkPhase > 0;

            float legSwing = 40f * run * s;
            float armSwing = 45f * run * s;
            if (!_pc.Grounded)
            {
                legSwing = 25f;
                armSwing = -60f;
            }
            _rig.LegL.localRotation = Quaternion.Euler(legSwing, 0, 0);
            _rig.LegR.localRotation = Quaternion.Euler(-legSwing, 0, 0);
            _rig.ArmL.localRotation = Quaternion.Euler(-armSwing, 0, -8);  // overridden below when handling the ball
            // Zander is left-handed: the LEFT arm shoots and dribbles
            _rig.ArmR.localRotation = Quaternion.Euler(armSwing, 0, 8);
            if (charging || shooting || cheering)
                _rig.ArmL.localRotation = Quaternion.Euler(-160, 0, -10);
            else if (ball && _pc.Grounded)
                _rig.ArmL.localRotation = Quaternion.Euler(-35 - 20 * Mathf.Abs(Mathf.Cos(_t * Mathf.PI * 2)), 0, -12);
            if (cheering || charging) _rig.ArmR.localRotation = Quaternion.Euler(-160, 0, 10);
            float bob = _pc.Grounded ? Mathf.Abs(Mathf.Sin(ph)) * 0.04f * run + Mathf.Sin(_t * 2f) * 0.01f : 0f;
            _rig.Body.localPosition = new Vector3(0, bob, 0);
            _rig.Body.localRotation = Quaternion.Euler(8f * run, 0, 0);
        }

        void OnDestroy() => _clips?.Destroy();
    }

    /// <summary>Pivot references for the primitive fallback characters.</summary>
    public class CharacterRig : MonoBehaviour
    {
        public Transform Body, Head, ArmL, ArmR, LegL, LegR, HandR;
    }
}
