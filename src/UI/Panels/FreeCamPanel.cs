using UnityExplorer.Config;
using UnityExplorer.Inspectors;
using UniverseLib.Input;
using UniverseLib.UI;
using UniverseLib.UI.Models;
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#endif
#if INTEROP
using Il2CppInterop.Runtime.Injection;
#endif

namespace UnityExplorer.UI.Panels
{
    public class FreeCamPanel : UEPanel
    {
        public FreeCamPanel(UIBase owner) : base(owner)
        {
        }

        public override string Name => "Freecam";
        public override UIManager.Panels PanelType => UIManager.Panels.Freecam;
        public override int MinWidth => 450;
        public override int MinHeight => 650;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;

        internal static bool inFreeCamMode;
        internal static bool usingGameCamera;
        public static Camera ourCamera;
        public static Camera lastMainCamera;
        internal static FreeCamBehaviour freeCamScript;
        internal static CatmullRom.CatmullRomMover cameraPathMover;

        internal static float desiredMoveSpeed = 5f;

        internal static Vector3 originalCameraPosition;
        internal static Quaternion originalCameraRotation;
        internal static float originalCameraFOV;

        internal static Vector3? currentUserCameraPosition;
        internal static Quaternion? currentUserCameraRotation;

        internal static Vector3 previousMousePosition;

        internal static Vector3 lastSetCameraPosition;

        static ButtonRef startStopButton;
        public static Toggle useGameCameraToggle;
        public static Toggle blockFreecamMovementToggle;
        static InputFieldRef positionInput;
        static InputFieldRef moveSpeedInput;
        static Text followObjectLabel;
        static ButtonRef inspectButton;
        static bool disabledCinemachine;

        public static GameObject followObject = null;
        internal static void BeginFreecam()
        {
            inFreeCamMode = true;

            previousMousePosition = InputManager.MousePosition;

            CacheMainCamera();
            SetupFreeCamera();

            inspectButton.GameObject.SetActive(true);
        }

        static void CacheMainCamera()
        {
            Camera currentMain = Camera.main;
            if (currentMain)
            {
                lastMainCamera = currentMain;
                originalCameraPosition = currentMain.transform.position;
                originalCameraRotation = currentMain.transform.rotation;
                originalCameraFOV = currentMain.fieldOfView;

                if (currentUserCameraPosition == null)
                {
                    currentUserCameraPosition = currentMain.transform.position;
                    currentUserCameraRotation = currentMain.transform.rotation;
                }
            }
            else
                originalCameraRotation = Quaternion.identity;
        }

        static void SetupFreeCamera()
        {
            if (useGameCameraToggle.isOn)
            {
                if (!lastMainCamera)
                {
                    ExplorerCore.LogWarning($"There is no previous Camera found, reverting to default Free Cam.");
                    useGameCameraToggle.isOn = false;
                }
                else
                {
                    usingGameCamera = true;
                    ourCamera = lastMainCamera;
                    MaybeToggleCinemachine(false);
                }
            }

            if (!useGameCameraToggle.isOn)
            {
                usingGameCamera = false;

                if (lastMainCamera)
                    lastMainCamera.enabled = false;
            }

            if (!ourCamera)
            {
                ourCamera = new GameObject("UE_Freecam").AddComponent<Camera>();
                ourCamera.gameObject.tag = "MainCamera";
                GameObject.DontDestroyOnLoad(ourCamera.gameObject);
                ourCamera.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            if (!freeCamScript)
                freeCamScript = ourCamera.gameObject.AddComponent<FreeCamBehaviour>();

            if (!cameraPathMover)
                cameraPathMover = ourCamera.gameObject.AddComponent<CatmullRom.CatmullRomMover>();

            ourCamera.transform.position = (Vector3)currentUserCameraPosition;
            ourCamera.transform.rotation = (Quaternion)currentUserCameraRotation;

            ourCamera.gameObject.SetActive(true);
            ourCamera.enabled = true;
        }

        internal static void EndFreecam()
        {
            inFreeCamMode = false;

            if (usingGameCamera)
            {

                MaybeToggleCinemachine(true);
                ourCamera = null;

                if (lastMainCamera)
                {
                    lastMainCamera.transform.position = originalCameraPosition;
                    lastMainCamera.transform.rotation = originalCameraRotation;
                    lastMainCamera.fieldOfView = originalCameraFOV;
                }
            }

            if (ourCamera)
                ourCamera.gameObject.SetActive(false);
            else
                inspectButton.GameObject.SetActive(false);

            if (freeCamScript)
            {
                GameObject.Destroy(freeCamScript);
                freeCamScript = null;
            }

            if (cameraPathMover)
            {
                GameObject.Destroy(cameraPathMover);
                cameraPathMover = null;
            }

            if (lastMainCamera)
                lastMainCamera.enabled = true;
        }

        // Experimental feature to automatically disable cinemachine when turning on the gameplay freecam.
        // If it causes problems in some games we should consider removing it or making it a toggle.
        // Also, if there are more generic Unity components that control the camera we should include them here.
        // Not sure if a cinemachine can be inside another gameobject and not in the maincamera component, but we should take that in mind if this doesn't work in some games.
        static void MaybeToggleCinemachine(bool enable){
            // If we want to enable cinemachine but never disabled don't even look for it
            if(enable && !disabledCinemachine)
                return;
            
            if (ourCamera){
                IEnumerable<Behaviour> comps = ourCamera.GetComponents<Behaviour>();
                foreach (Behaviour comp in comps)
                {
                    if (comp.GetType().ToString() == "Cinemachine.CinemachineBrain"){
                        comp.enabled = enable;
                        disabledCinemachine = !enable;
                        break;
                    }
                }
            }
        }

        static void SetCameraPosition(Vector3 pos)
        {
            if (!ourCamera || lastSetCameraPosition == pos)
                return;

            ourCamera.transform.position = pos;
            lastSetCameraPosition = pos;
        }

        internal static void UpdatePositionInput()
        {
            if (!ourCamera)
                return;

            if (positionInput.Component.isFocused)
                return;

            lastSetCameraPosition = ourCamera.transform.position;
            positionInput.Text = ParseUtility.ToStringForInput<Vector3>(lastSetCameraPosition);
        }

        // ~~~~~~~~ UI construction / callbacks ~~~~~~~~

        protected override void ConstructPanelContent()
        {
            startStopButton = UIFactory.CreateButton(ContentRoot, "ToggleButton", "Freecam");
            UIFactory.SetLayoutElement(startStopButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            startStopButton.OnClick += StartStopButton_OnClick;
            SetToggleButtonState();

            AddSpacer(5);

            GameObject toggleObj = UIFactory.CreateToggle(ContentRoot, "UseGameCameraToggle", out useGameCameraToggle, out Text useGameCameraText);
            UIFactory.SetLayoutElement(toggleObj, minHeight: 25, flexibleWidth: 9999);
            useGameCameraToggle.onValueChanged.AddListener(OnUseGameCameraToggled);
            useGameCameraToggle.isOn = false;
            useGameCameraText.text = "Use Game Camera?";

            AddSpacer(5);

            GameObject posRow = AddInputField("Position", "Freecam Pos:", "eg. 0 0 0", out positionInput, PositionInput_OnEndEdit);

            ButtonRef resetPosButton = UIFactory.CreateButton(posRow, "ResetButton", "Reset");
            UIFactory.SetLayoutElement(resetPosButton.GameObject, minWidth: 70, minHeight: 25);
            resetPosButton.OnClick += OnResetPosButtonClicked;

            AddSpacer(5);

            AddInputField("MoveSpeed", "Move Speed:", "Default: 1", out moveSpeedInput, MoveSpeedInput_OnEndEdit);
            moveSpeedInput.Text = desiredMoveSpeed.ToString();

            AddSpacer(5);

            followObjectLabel = UIFactory.CreateLabel(ContentRoot, "CurrentFollowObject", "Not following any object.");
            UIFactory.SetLayoutElement(followObjectLabel.gameObject, minWidth: 100, minHeight: 25);

            GameObject followObjectRow = UIFactory.CreateHorizontalGroup(ContentRoot, $"FollowObjectRow", false, false, true, true, 3, default, new(1, 1, 1, 0));

            ButtonRef followButton = UIFactory.CreateButton(followObjectRow, "FollowButton", "Follow GameObject");
            UIFactory.SetLayoutElement(followButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            followButton.OnClick += FollowButton_OnClick;

            ButtonRef releaseFollowButton = UIFactory.CreateButton(followObjectRow, "ReleaseFollowButton", "Release Follow GameObject");
            UIFactory.SetLayoutElement(releaseFollowButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            releaseFollowButton.OnClick += ReleaseFollowButton_OnClick;

            AddSpacer(5);

            GameObject blockFreecamMovement = UIFactory.CreateToggle(ContentRoot, "blockFreecamMovement", out blockFreecamMovementToggle, out Text blockFreecamMovementText);
            UIFactory.SetLayoutElement(blockFreecamMovement, minHeight: 25, flexibleWidth: 9999);
            blockFreecamMovementToggle.isOn = false;
            blockFreecamMovementText.text = "Block Freecam movement";

            AddSpacer(5);


            string instructions = "Controls:\n" +
            $"- {ConfigManager.Forwards_1.Value},{ConfigManager.Backwards_1.Value},{ConfigManager.Left_1.Value},{ConfigManager.Right_1.Value} / {ConfigManager.Forwards_2.Value},{ConfigManager.Backwards_2.Value},{ConfigManager.Left_2.Value},{ConfigManager.Right_2.Value}: Movement\n" +
            $"- {ConfigManager.Up.Value}: Move up\n" +
            $"- {ConfigManager.Down.Value}: Move down\n" +
            $"- {ConfigManager.Tilt_Left.Value} / {ConfigManager.Tilt_Right.Value}: Tilt \n" +
            $"- Right Mouse Button: Free look\n" +
            $"- {ConfigManager.Speed_Up_Movement.Value}: Super speed\n" +
            $"- {ConfigManager.Speed_Down_Movement.Value}: Slow speed\n" +
            $"- {ConfigManager.Increase_FOV.Value} / {ConfigManager.Decrease_FOV.Value}: Change FOV\n" +
            $"- {ConfigManager.Tilt_Reset.Value}: Reset tilt\n" +
            $"- {ConfigManager.Reset_FOV.Value}: Reset FOV\n\n" +
            "Extra:\n" +
            $"- {ConfigManager.Freecam_Toggle.Value}: Freecam toggle\n" +
            $"- {ConfigManager.Block_Freecam_Movement.Value}: Block freecam movement\n" +
            $"- {ConfigManager.HUD_Toggle.Value}: HUD toggle\n" +
            $"- {ConfigManager.Pause.Value}: Pause\n" +
            $"- {ConfigManager.Frameskip.Value}: Frameskip\n";

            if (ConfigManager.Frameskip.Value != KeyCode.None) instructions = instructions + $"- {ConfigManager.Screenshot.Value}: Screenshot\n";

            Text instructionsText = UIFactory.CreateLabel(ContentRoot, "Instructions", instructions, TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(instructionsText.gameObject, flexibleWidth: 9999, flexibleHeight: 9999);

            AddSpacer(5);

            inspectButton = UIFactory.CreateButton(ContentRoot, "InspectButton", "Inspect Free Camera");
            UIFactory.SetLayoutElement(inspectButton.GameObject, flexibleWidth: 9999, minHeight: 25);
            inspectButton.OnClick += () => { InspectorManager.Inspect(ourCamera); };
            inspectButton.GameObject.SetActive(false);

            AddSpacer(5);
        }

        void AddSpacer(int height)
        {
            GameObject obj = UIFactory.CreateUIObject("Spacer", ContentRoot);
            UIFactory.SetLayoutElement(obj, minHeight: height, flexibleHeight: 0);
        }

        GameObject AddInputField(string name, string labelText, string placeHolder, out InputFieldRef inputField, Action<string> onInputEndEdit)
        {
            GameObject row = UIFactory.CreateHorizontalGroup(ContentRoot, $"{name}_Group", false, false, true, true, 3, default, new(1, 1, 1, 0));

            Text posLabel = UIFactory.CreateLabel(row, $"{name}_Label", labelText);
            UIFactory.SetLayoutElement(posLabel.gameObject, minWidth: 100, minHeight: 25);

            inputField = UIFactory.CreateInputField(row, $"{name}_Input", placeHolder);
            UIFactory.SetLayoutElement(inputField.GameObject, minWidth: 125, minHeight: 25, flexibleWidth: 9999);
            inputField.Component.GetOnEndEdit().AddListener(onInputEndEdit);

            return row;
        }

        public static void StartStopButton_OnClick()
        {
            EventSystemHelper.SetSelectedGameObject(null);

            if (inFreeCamMode)
                EndFreecam();
            else
                BeginFreecam();

            SetToggleButtonState();
        }

        public static void FollowObjectAction(GameObject obj){
            followObject = obj;
            followObjectLabel.text = $"Following: {obj.name}";
        }

        void FollowButton_OnClick()
        {
            MouseInspector.Instance.StartInspect(MouseInspectMode.World, FollowObjectAction);
        }

        void ReleaseFollowButton_OnClick()
        {
            if (followObject && ourCamera.transform.IsChildOf(followObject.transform)){
                ourCamera.transform.SetParent(null, true);
                followObject = null;
                followObjectLabel.text = "Not following any object";
            }
        }

        static void SetToggleButtonState()
        {
            if (inFreeCamMode)
            {
                RuntimeHelper.SetColorBlockAuto(startStopButton.Component, new(0.4f, 0.2f, 0.2f));
                startStopButton.ButtonText.text = "End Freecam";
            }
            else
            {
                RuntimeHelper.SetColorBlockAuto(startStopButton.Component, new(0.2f, 0.4f, 0.2f));
                startStopButton.ButtonText.text = "Begin Freecam";
            }
        }

        void OnUseGameCameraToggled(bool value)
        {
            // If the previous camera is following a game object we remove it from tis childs.
            if (followObject && ourCamera.transform.IsChildOf(followObject.transform)){
                ourCamera.transform.SetParent(null, true);
            }

            EventSystemHelper.SetSelectedGameObject(null);

            if (!inFreeCamMode)
                return;

            EndFreecam();
            BeginFreecam();
        }

        void OnResetPosButtonClicked()
        {
            currentUserCameraPosition = originalCameraPosition;
            currentUserCameraRotation = originalCameraRotation;

            if (inFreeCamMode && ourCamera)
            {
                ourCamera.transform.position = (Vector3)currentUserCameraPosition;
                ourCamera.transform.rotation = (Quaternion)currentUserCameraRotation;
                ourCamera.fieldOfView = originalCameraFOV;
            }

            positionInput.Text = ParseUtility.ToStringForInput<Vector3>(originalCameraPosition);
        }

        void PositionInput_OnEndEdit(string input)
        {
            EventSystemHelper.SetSelectedGameObject(null);

            if (!ParseUtility.TryParse(input, out Vector3 parsed, out Exception parseEx))
            {
                ExplorerCore.LogWarning($"Could not parse position to Vector3: {parseEx.ReflectionExToString()}");
                UpdatePositionInput();
                return;
            }

            SetCameraPosition(parsed);
        }

        void MoveSpeedInput_OnEndEdit(string input)
        {
            EventSystemHelper.SetSelectedGameObject(null);

            if (!ParseUtility.TryParse(input, out float parsed, out Exception parseEx))
            {
                ExplorerCore.LogWarning($"Could not parse value: {parseEx.ReflectionExToString()}");
                moveSpeedInput.Text = desiredMoveSpeed.ToString();
                return;
            }

            desiredMoveSpeed = parsed;
        }
    }

    internal class FreeCamBehaviour : MonoBehaviour
    {
#if CPP
        static FreeCamBehaviour()
        {
            ClassInjector.RegisterTypeInIl2Cpp<FreeCamBehaviour>();
        }

        public FreeCamBehaviour(IntPtr ptr) : base(ptr) { }
#endif

        internal void Update()
        {
            if (FreeCamPanel.inFreeCamMode)
            {
                if (!FreeCamPanel.ourCamera)
                {
                    FreeCamPanel.EndFreecam();
                    return;
                }

                if (FreeCamPanel.followObject && !FreeCamPanel.ourCamera.transform.IsChildOf(FreeCamPanel.followObject.transform)){
                    FreeCamPanel.ourCamera.transform.SetParent(FreeCamPanel.followObject.transform, true);
                }



                // ------------- Handle input ----------------

                if (FreeCamPanel.blockFreecamMovementToggle.isOn || FreeCamPanel.cameraPathMover.playingPath){
                    return;
                }

                Transform transform = FreeCamPanel.ourCamera.transform;

                FreeCamPanel.currentUserCameraPosition = transform.position;
                FreeCamPanel.currentUserCameraRotation = transform.rotation;

                float moveSpeed = FreeCamPanel.desiredMoveSpeed * 0.01665f; //"0.01665f" (60fps) in place of Time.DeltaTime. DeltaTime causes issues when game is paused.
                float speedModifier = 1;
                if (InputManager.GetKey(ConfigManager.Speed_Up_Movement.Value))
                    speedModifier = 10f;

                if (InputManager.GetKey(ConfigManager.Speed_Down_Movement.Value))
                    speedModifier = 0.1f;

                moveSpeed *= speedModifier;

                if (InputManager.GetKey(ConfigManager.Left_1.Value) || InputManager.GetKey(ConfigManager.Left_2.Value))
                    transform.position += transform.right * -1 * moveSpeed;

                if (InputManager.GetKey(ConfigManager.Right_1.Value) || InputManager.GetKey(ConfigManager.Right_2.Value))
                    transform.position += transform.right * moveSpeed;

                if (InputManager.GetKey(ConfigManager.Forwards_1.Value) || InputManager.GetKey(ConfigManager.Forwards_2.Value))
                    transform.position += transform.forward * moveSpeed;

                if (InputManager.GetKey(ConfigManager.Backwards_1.Value) || InputManager.GetKey(ConfigManager.Backwards_2.Value))
                    transform.position += transform.forward * -1 * moveSpeed;

                if (InputManager.GetKey(ConfigManager.Up.Value))
                    transform.position += transform.up * moveSpeed;

                if (InputManager.GetKey(ConfigManager.Down.Value))
                    transform.position += transform.up * -1 * moveSpeed;

                if (InputManager.GetKey(ConfigManager.Tilt_Left.Value))
                    transform.Rotate(0, 0, moveSpeed, Space.Self);

                if (InputManager.GetKey(ConfigManager.Tilt_Right.Value))
                    transform.Rotate(0, 0, - moveSpeed, Space.Self);

                if (InputManager.GetKey(ConfigManager.Tilt_Reset.Value)){
                    // Extract the forward direction of the original quaternion
                    Vector3 forwardDirection = transform.rotation * Vector3.forward;
                    // Reset the tilt by creating a new quaternion with no tilt
                    Quaternion newRotation = Quaternion.LookRotation(forwardDirection, Vector3.up);

                    transform.rotation = newRotation;
                }

                if (InputManager.GetMouseButton(1))
                {
                    Vector3 mouseDelta = InputManager.MousePosition - FreeCamPanel.previousMousePosition;
                    if (mouseDelta.x != 0 || mouseDelta.y != 0){
                        // Calculate the mouse movement vector depending on the current camera tilt.
                        float tiltAngle = transform.eulerAngles.z;
                        const float PI = 3.141592f;
                        float dirAngle = Mathf.Atan2(mouseDelta.y, mouseDelta.x);
                        dirAngle *= 180 / PI;
                        float newAngle = (dirAngle + tiltAngle) * PI / 180;
                        Vector2 newMouseCoords = new Vector2(Mathf.Cos(newAngle), Mathf.Sin(newAngle) ).normalized * 2f * (speedModifier == 10 ? 3 : speedModifier);

                        float newRotationX = transform.localEulerAngles.y + newMouseCoords.x;
                        float newRotationY = transform.localEulerAngles.x - newMouseCoords.y;
                        transform.localEulerAngles = new Vector3(newRotationY, newRotationX, transform.localEulerAngles.z);
                    }

                    FreeCamPanel.previousMousePosition = InputManager.MousePosition;
                }

                if (InputManager.GetKey(ConfigManager.Decrease_FOV.Value))
                {
                    FreeCamPanel.ourCamera.fieldOfView -= moveSpeed; 
                }

                if (InputManager.GetKey(ConfigManager.Increase_FOV.Value))
                {
                    FreeCamPanel.ourCamera.fieldOfView += moveSpeed; 
                }

                if (InputManager.GetKey(ConfigManager.Reset_FOV.Value)){
                    FreeCamPanel.ourCamera.fieldOfView = FreeCamPanel.usingGameCamera ? FreeCamPanel.originalCameraFOV : 60;
                }

                FreeCamPanel.UpdatePositionInput();

                FreeCamPanel.previousMousePosition = InputManager.MousePosition;
            }
        }
    }
}
