using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Every control in one place (keyboard + mouse, with basic gamepad support
    /// through Unity's default Input Manager axes and joystick buttons).
    ///
    ///   Move ............ WASD / arrow keys / left stick
    ///   Look ............ mouse
    ///   Jump ............ Space / A (button 0)
    ///   Sprint .......... Left Shift
    ///   Shoot (hold) .... Left mouse / F / X (button 2) - release in the GREEN zone!
    ///   Dunk ............ press Shoot in the air next to a hoop
    ///   Drop / place .... Q / right mouse / B (button 1)
    ///   Interact ........ E / Y (button 3)  (turn boards, pick a ball off a plate)
    ///   Hint ............ H / Back (button 6)
    ///   Super shots ..... Tab (every shot is perfect - great for little players)
    ///   Restart room .... R
    ///   Pause ........... Esc / Start (button 7)
    /// </summary>
    public static class Controls
    {
        public static Vector2 Move
        {
            get
            {
                var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static Vector2 Look => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        public static float Zoom => Input.GetAxis("Mouse ScrollWheel");

        public static bool JumpDown => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool Sprint => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton8);

        public static bool ShootDown => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F) ||
                                        Input.GetKeyDown(KeyCode.JoystickButton2);

        public static bool ShootHeld => Input.GetMouseButton(0) || Input.GetKey(KeyCode.F) ||
                                        Input.GetKey(KeyCode.JoystickButton2);

        public static bool DropDown => Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1) ||
                                       Input.GetKeyDown(KeyCode.JoystickButton1);

        public static bool InteractDown => Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton3);
        public static bool HintDown => Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.JoystickButton6);
        public static bool EasyToggleDown => Input.GetKeyDown(KeyCode.Tab);
        public static bool RestartDown => Input.GetKeyDown(KeyCode.R);
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
        public static bool AnyStart => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) ||
                                       Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0) ||
                                       Input.GetKeyDown(KeyCode.JoystickButton7);
    }
}
