using System.Linq;
using UnityEngine;
using SharpDX.DirectInput;

namespace UnityExplorer
{

    internal enum JoystickButtons
    {
        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        L1 = 4,
        R1 = 5,
        Select = 6,
        Start = 7,
        L3 = 8,
        R3 = 9,
    }

    internal enum JoystickDPad
    {
        Up = 0,
        Right = 9000,
        Down = 18000,
        Left = 27000,
    }
        
    internal class JoystickHandler{
        private DirectInput directInput;
        private Joystick joystick;
        private JoystickState currentState;

        private float deadzone = 0.3f;

        public void SetupController(int newControllerNumber){
            directInput = new DirectInput();
            var devices = directInput.GetDevices(SharpDX.DirectInput.DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices);
            if (devices.Count > 0)
            {
                if (newControllerNumber > devices.Count)
                    return;

                joystick = new Joystick(directInput, devices[newControllerNumber].InstanceGuid);
                joystick.Acquire();
            }
        }

        public void UpdateState() {
            // Poll the joystick for input
            if (joystick != null)
            {
                joystick.Poll();
                currentState = joystick.GetCurrentState();
            }
        }

        void OnDestroy()
        {
            // Release resources
            if (joystick != null)
            {
                joystick.Unacquire();
                joystick.Dispose();
            }
            if (directInput != null)
            {
                directInput.Dispose();
            }
        }

        public float GetLeftThumbX() {
            if (joystick == null) return 0;

            float returnValue = (currentState.X - 32767f) / 32767f;
            return Math.Abs(returnValue) < deadzone ? 0 : returnValue;
        }

        public float GetLeftThumbY() {
            if (joystick == null) return 0;

            float returnValue = (currentState.Y - 32767f) / 32767f;
            return Math.Abs(returnValue) < deadzone ? 0 : - returnValue;
        }

        public float GetRightThumbX() {
            if (joystick == null) return 0;

            float returnValue = (currentState.RotationX - 32767f) / 32767f;
            return Math.Abs(returnValue) < deadzone ? 0 : returnValue;
        }

        public float GetRightThumbY() {
            if (joystick == null) return 0;

            float returnValue = (currentState.RotationY - 32767f) / 32767f;
            return Math.Abs(returnValue) < deadzone ? 0 : - returnValue;
        }

        public float GetTriggers() {
            if (joystick == null) return 0;

            return (currentState.Z - 32767f) / 32767f;
        }

        public bool GetDPad(JoystickDPad input) {
            if (joystick == null) return false;
            int currentDPadValue = currentState.PointOfViewControllers[0];
            if (currentDPadValue == -1) return false;

            // currentDPadValue behaves like a thumbstick, so pressing more than one DPad button at the same time would result
            // in a number representing a "vector" pointing between those two buttons.
            switch (input){
                case JoystickDPad.Up:
                    return currentDPadValue < (int)JoystickDPad.Right || currentDPadValue > (int)JoystickDPad.Left;
                case JoystickDPad.Right:
                    return currentDPadValue < (int)JoystickDPad.Down && currentDPadValue > (int)JoystickDPad.Up;
                case JoystickDPad.Down:
                    return currentDPadValue < (int)JoystickDPad.Left && currentDPadValue > (int)JoystickDPad.Right;
                case JoystickDPad.Left:
                    return currentDPadValue > (int)JoystickDPad.Down;
            }
            return false;
        }

        public bool GetButton(JoystickButtons button) {
            if (joystick == null) return false;

            return currentState.Buttons[(int)button];
        }
    }
}
