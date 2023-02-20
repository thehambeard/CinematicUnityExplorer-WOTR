using UniverseLib.Input;
using UniverseLib.UI;
using UniverseLib.UI.Models;
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#endif
#if INTEROP
using Il2CppInterop.Runtime.Injection;
#endif

using UnityEngine.Rendering;
using UnityEngine;

namespace UnityExplorer.UI.Panels
{
    internal class LightsManager : UEPanel
    {
        public LightsManager(UIBase owner) : base(owner)
        {
        }

        public override string Name => "Lights Manager";
        public override UIManager.Panels PanelType => UIManager.Panels.LightsManager;
        public override int MinWidth => 400;
        public override int MinHeight => 320;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;

        static ButtonRef createLightButton;

        List<GameObject> CreatedLights = new List<GameObject>();

        // ~~~~~~~~ UI construction / callbacks ~~~~~~~~

        //private PostProcessVolume volume;
        //private UniverseLib.UnityEngine.Rendering.Fog fog;

        protected override void ConstructPanelContent()
        {
            //var getVolume = GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
            //volume = getVolume;
            //volume.profile.TryGetSettings(out fog);

            createLightButton = UIFactory.CreateButton(ContentRoot, "ToggleButton", "Create Light");
            UIFactory.SetLayoutElement(createLightButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            createLightButton.OnClick += CreateLight;

            ListCreatedLights();
        }

        private void ListCreatedLights(){
            foreach (var light in CreatedLights) {
                DrawOptionsLights(light);
            }
        }

        private void DrawOptionsLights(GameObject light){
            //Active
            Toggle useGameCameraToggle = new Toggle();

            GameObject toggleObj = UIFactory.CreateToggle(ContentRoot, "UseGameCameraToggle", out useGameCameraToggle, out Text toggleText);
            UIFactory.SetLayoutElement(toggleObj, minHeight: 25, flexibleWidth: 9999);
            useGameCameraToggle.onValueChanged.AddListener(value => { light.SetActive(value); });
            useGameCameraToggle.isOn = true;
            toggleText.text = "Activate";

            AddSpacer(5);

            //Color
            //GameObject color = (new CacheObject.IValues.InteractiveColor()).CreateContent(light);
            InputFieldRef colorInput;

            GameObject color = AddInputField("Color", "Color:", "eg. 1 1 1", out colorInput, (input) => { ColorInput_OnEndEdit(light, input); });

            AddSpacer(5);
        }

        private void CreateLight(){
            GameObject obj = new("UE - Light");
            //DontDestroyOnLoad(obj);
            obj.hideFlags = HideFlags.HideAndDontSave;
            obj.AddComponent<UnityEngine.Light>();

            Transform freeCamTransform = FreeCamTransform();

            obj.transform.position = freeCamTransform.position;
            obj.transform.rotation = freeCamTransform.rotation;

            CreatedLights.Add(obj);
            ConstructPanelContent();
        }

        private Transform FreeCamTransform(){
            Camera freeCam = FreeCamPanel.ourCamera;
            
            if (freeCam != null) {
                return freeCam.transform;
            }
            else
                return new Transform();
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


        void ColorInput_OnEndEdit(GameObject ob, string input)
        {
            EventSystemHelper.SetSelectedGameObject(null);

            if (!ParseUtility.TryParse(input, out Vector3 parsed, out Exception parseEx))
            {
                ExplorerCore.LogWarning($"Could not parse value: {parseEx.ReflectionExToString()}");
                //positionInput.Text = ParseUtility.ToStringForInput<Vector3>(lastSetCameraPosition);
                return;
            }

            ob.GetComponent<Light>().color = new Color(parsed.x, parsed.y, parsed.z, 1f);
        }
    }
}
