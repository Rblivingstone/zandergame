using System.Runtime.InteropServices;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Every control in one place: keyboard + mouse AND an Xbox-layout (XInput)
    /// controller such as the GameSir Kaleid wired controller on Windows.
    ///
    ///   Action              Keyboard / mouse          Controller (Xbox layout)
    ///   ------------------  ------------------------  ------------------------
    ///   Move                WASD / arrows             Left stick (or D-pad)
    ///   Look                Mouse                     Right stick
    ///   Jump                Space                     A
    ///   Dash (stamina)      Left Shift                LT (hold) or L3
    ///   Shoot (hold)        Left mouse / F            RT (hold) - let go in the GREEN zone!
    ///   Dunk (hold = hang)  C                         X   (near a hoop, holding the ball)
    ///   Drop / place ball   Q / right mouse           B
    ///   Use / turn board    E                         Y
    ///   Camera behind       -                         RB (or R3)
    ///   Hint                H                         LB
    ///   Super Shots         Tab                       View (two squares)
    ///   Pause               Esc                       Menu (three lines)
    ///   Restart room        P (away from E)           Pause menu
    ///
    /// The controller axes are defined in ProjectSettings/InputManager.asset
    /// (ZG_LeftX, ZG_LeftY, ZG_RightX, ZG_RightY, ZG_LT, ZG_RT, ZG_DPadX, ZG_DPadY).
    /// On-screen text uses tokens like {SHOOT} that Expand() turns into the
    /// button for whichever device was used last.
    /// </summary>
    public static class Controls
    {
        // Xbox / XInput button numbers on Windows
        const KeyCode PadA = KeyCode.JoystickButton0;
        const KeyCode PadB = KeyCode.JoystickButton1;
        const KeyCode PadX = KeyCode.JoystickButton2;
        const KeyCode PadY = KeyCode.JoystickButton3;
        const KeyCode PadLB = KeyCode.JoystickButton4;
        const KeyCode PadRB = KeyCode.JoystickButton5;
        const KeyCode PadView = KeyCode.JoystickButton6;
        const KeyCode PadMenu = KeyCode.JoystickButton7;
        const KeyCode PadL3 = KeyCode.JoystickButton8;
        const KeyCode PadR3 = KeyCode.JoystickButton9;

        public const float StickDeadzone = 0.15f;   // Hall-effect sticks barely drift
        public const float TriggerPress = 0.45f;
        public const float LookSpeed = 170f;        // degrees per second at full right stick

        /// <summary>True when the controller was the last device used (switches prompts).</summary>
        public static bool UsingGamepad { get; private set; }

        static int _frame = -1;
        static bool _rtHeld, _rtDown, _ltHeld;
        static float _dpadXPrev, _dpadYPrev, _stickYPrev;
        static int _menuNav;
        static Vector2 _leftStick, _rightStick;

        // ------------------------------------------------------------------ core
        static float Axis(string name)
        {
            try { return Input.GetAxisRaw(name); }
            catch (System.ArgumentException) { return 0f; }   // axis not defined in InputManager
        }

        static Vector2 Radial(float x, float y)
        {
            var v = new Vector2(x, y);
            float m = v.magnitude;
            if (m < StickDeadzone) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - StickDeadzone) / (1f - StickDeadzone));
        }

        /// <summary>Updates edge states once per frame. Called by ControlsDriver (early) and lazily.</summary>
        public static void Tick()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;

            _leftStick = Radial(Axis("ZG_LeftX"), Axis("ZG_LeftY"));
            _rightStick = Radial(Axis("ZG_RightX"), Axis("ZG_RightY"));

            float rt = Axis("ZG_RT"), lt = Axis("ZG_LT");
            bool rtNow = rt > TriggerPress;
            _rtDown = rtNow && !_rtHeld;
            _rtHeld = rtNow;
            _ltHeld = lt > TriggerPress;

            // menu navigation (D-pad or left stick, edge-triggered)
            float dy = Axis("ZG_DPadY");
            float sy = _leftStick.y;
            _menuNav = 0;
            if (dy > 0.5f && _dpadYPrev <= 0.5f || sy > 0.6f && _stickYPrev <= 0.6f) _menuNav = -1;
            if (dy < -0.5f && _dpadYPrev >= -0.5f || sy < -0.6f && _stickYPrev >= -0.6f) _menuNav = 1;
            _dpadYPrev = dy;
            _stickYPrev = sy;
            _dpadXPrev = Axis("ZG_DPadX");

            // which device was used last?
            bool padActivity = _leftStick.sqrMagnitude > 0.09f || _rightStick.sqrMagnitude > 0.09f || rt > 0.3f || lt > 0.3f ||
                               Mathf.Abs(dy) > 0.5f || Mathf.Abs(_dpadXPrev) > 0.5f;
            for (int i = 0; i < 12 && !padActivity; i++)
                if (Input.GetKey(KeyCode.JoystickButton0 + i)) padActivity = true;
            if (padActivity) UsingGamepad = true;
            else if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) ||
                     Mathf.Abs(Axis("Mouse X")) + Mathf.Abs(Axis("Mouse Y")) > 0.5f ||
                     (Input.anyKeyDown && !AnyPadButtonDown()))
                UsingGamepad = false;

            Rumble.Tick();
        }

        static bool AnyPadButtonDown()
        {
            for (int i = 0; i < 20; i++)
                if (Input.GetKeyDown(KeyCode.JoystickButton0 + i)) return true;
            return false;
        }

        // ------------------------------------------------------------- gameplay
        public static Vector2 Move
        {
            get
            {
                Tick();
                var v = new Vector2(Axis("Horizontal"), Axis("Vertical"));
                v += _leftStick;
                v += new Vector2(Axis("ZG_DPadX"), Axis("ZG_DPadY"));
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        /// <summary>Mouse look (per-frame delta, already scaled by the camera's sensitivity).</summary>
        public static Vector2 MouseLook => new Vector2(Axis("Mouse X"), Axis("Mouse Y"));

        /// <summary>Right stick, -1..1 (up = +1).</summary>
        public static Vector2 StickLook
        {
            get
            {
                Tick();
                return _rightStick;
            }
        }

        public static float Zoom => Axis("Mouse ScrollWheel");

        public static bool JumpDown => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(PadA);

        public static bool Sprint
        {
            get
            {
                Tick();
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(PadL3) || _ltHeld;
            }
        }

        public static bool ShootDown
        {
            get
            {
                Tick();
                return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F) || _rtDown;
            }
        }

        public static bool ShootHeld
        {
            get
            {
                Tick();
                return Input.GetMouseButton(0) || Input.GetKey(KeyCode.F) || _rtHeld;
            }
        }

        /// <summary>Dunk has its own button: X on the controller, C on the keyboard.</summary>
        public static bool DunkDown => Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(PadX);
        public static bool DunkHeld => Input.GetKey(KeyCode.C) || Input.GetKey(PadX);

        public static bool DropDown => Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1) || Input.GetKeyDown(PadB);
        public static bool InteractDown => Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(PadY);
        public static bool HintDown => Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(PadLB);
        public static bool EasyToggleDown => Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(PadView);
        // P, not R: R sits right next to E (steal / use) and was too easy to hit by accident
        public static bool RestartDown => Input.GetKeyDown(KeyCode.P);
        public static bool RecenterDown => Input.GetKeyDown(PadRB) || Input.GetKeyDown(PadR3);
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(PadMenu);

        public static bool AnyStart => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) ||
                                       Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(PadA) || Input.GetKeyDown(PadMenu);

        // ---------------------------------------------------------------- menus
        /// <summary>-1 = up, +1 = down, 0 = nothing (D-pad, left stick, arrow keys, W/S).</summary>
        public static int MenuNav
        {
            get
            {
                Tick();
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return -1;
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return 1;
                return _menuNav;
            }
        }

        public static bool MenuConfirm => Input.GetKeyDown(PadA) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        public static bool MenuBack => Input.GetKeyDown(PadB);

        // ---------------------------------------------------------------- labels
        public static string Label(string action)
        {
            bool pad = UsingGamepad;
            switch (action)
            {
                case "SHOOT": return pad ? "RT" : "LEFT MOUSE";
                case "DUNK": return pad ? "X" : "C";
                case "JUMP": return pad ? "A" : "SPACE";
                case "DROP": return pad ? "B" : "Q";
                case "USE": return pad ? "Y" : "E";
                case "HINT": return pad ? "LB" : "H";
                case "EASY": return pad ? "VIEW" : "TAB";
                case "SPRINT": return pad ? "LT" : "SHIFT";
                case "PAUSE": return pad ? "MENU" : "ESC";
                case "MOVE": return pad ? "LEFT STICK" : "WASD";
                case "LOOK": return pad ? "RIGHT STICK" : "MOUSE";
                case "CAMERA": return pad ? "RB" : "MOUSE";
                case "RESTART": return pad ? "the PAUSE menu" : "P";
                case "START": return pad ? "A" : "SPACE";
                default: return action;
            }
        }

        /// <summary>Replaces {SHOOT}, {USE}, ... with the right button names.</summary>
        public static string Expand(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            foreach (var key in new[] { "SHOOT", "DUNK", "JUMP", "DROP", "USE", "HINT", "EASY", "SPRINT", "PAUSE", "MOVE", "LOOK",
                         "CAMERA", "RESTART", "START" })
                text = text.Replace("{" + key + "}", Label(key));
            return text;
        }
    }

    /// <summary>Runs Controls.Tick() before any other script each frame.</summary>
    [DefaultExecutionOrder(-1000)]
    public class ControlsDriver : MonoBehaviour
    {
        void Update() => Controls.Tick();
        void OnApplicationQuit() => Rumble.Stop();
        void OnDisable() => Rumble.Stop();
    }

    /// <summary>
    /// Controller vibration through XInput (Windows). Does nothing on other
    /// platforms or if no XInput controller is connected.
    /// </summary>
    public static class Rumble
    {
        public static bool Enabled = true;
        static float _until;
        static bool _active;
        static bool _broken;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        struct XInputVibration
        {
            public ushort LeftMotor;
            public ushort RightMotor;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        static extern uint SetState14(uint userIndex, ref XInputVibration vibration);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
        static extern uint SetState910(uint userIndex, ref XInputVibration vibration);

        static bool _use910;

        static void Send(float low, float high)
        {
            if (_broken) return;
            var v = new XInputVibration
            {
                LeftMotor = (ushort)(Mathf.Clamp01(low) * 65535),
                RightMotor = (ushort)(Mathf.Clamp01(high) * 65535)
            };
            try
            {
                if (!_use910) SetState14(0, ref v);
                else SetState910(0, ref v);
            }
            catch (System.DllNotFoundException)
            {
                if (_use910) _broken = true;
                else
                {
                    _use910 = true;
                    Send(low, high);
                }
            }
            catch (System.Exception)
            {
                _broken = true;
            }
        }
#else
        static void Send(float low, float high) { }
#endif

        /// <summary>Buzz the controller. low = big motor (thud), high = small motor (buzz).</summary>
        public static void Pulse(float low, float high, float seconds)
        {
            if (!Enabled || !Controls.UsingGamepad) return;
            Send(low, high);
            _active = true;
            _until = Mathf.Max(_until, Time.unscaledTime + seconds);
        }

        public static void Tick()
        {
            if (_active && Time.unscaledTime > _until) Stop();
        }

        public static void Stop()
        {
            if (!_active) return;
            _active = false;
            Send(0, 0);
        }
    }
}
