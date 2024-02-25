using HarmonyLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
#if UNHOLLOWER
using IL2CPPUtils = UnhollowerBaseLib.UnhollowerUtils;
#endif
#if INTEROP
using IL2CPPUtils = Il2CppInterop.Common.Il2CppInteropUtils;
#endif

namespace UnityExplorer.UI.Widgets
{
    public class TimeScaleWidget
    {
        public TimeScaleWidget(GameObject parent)
        {
            Instance = this;

            ConstructUI(parent);

            InitPatch();
        }

        static TimeScaleWidget Instance;

        ButtonRef lockBtn;
        bool locked;
        InputFieldRef timeInput;
        float desiredTime = 1;
        bool settingTimeScale;
        bool pause;
        Slider slider;

        bool shouldSliderUnpauseOnValueChange = true;
        float previousDesiredTime;
        bool previousLocked;

        public void Update()
        {
            // Force the timescale in case the game tries force it for us
            if (locked)
                SetTimeScale(desiredTime);

            //if (!timeInput.Component.isFocused)
            //    timeInput.Text = Time.timeScale.ToString("F2");
        }

        public void PauseToggle(){
            // If not paused but moved the slider to 0, consider that as it being paused
            if (desiredTime == 0 && locked && !pause) pause = true;

            pause = !pause;

            locked = pause ? true : previousLocked;
            UpdateLockedButton();
            desiredTime = pause ? 0f : previousDesiredTime;
            // We assume the vanilla game speed was 1f before editing it
            SetTimeScale(locked ? desiredTime : 1f);

            shouldSliderUnpauseOnValueChange = false;
            slider.value = desiredTime;
            shouldSliderUnpauseOnValueChange = true;
        }

        public bool IsPaused(){
            return pause;
        }

        public void SetTimeScale(float time)
        {
            settingTimeScale = true;
            Time.timeScale = time;
            settingTimeScale = false;
        }

        // UI event listeners

        void OnTimeInputEndEdit(string val)
        {
            if (float.TryParse(val, out float f))
            {
                if (f < slider.minValue || f > slider.maxValue){
                    ExplorerCore.LogWarning("Error, new time scale value outside of margins.");
                    timeInput.Text = desiredTime.ToString("0.00");
                    return;
                }

                slider.value = f; // Will update the desiredTime value and extra things
            }
        }

        void OnLockedButtonClicked()
        {
            locked = !locked;
            UpdateLockedButton();
            previousLocked = locked;

            // If the game was paused we consider this an unpause
            if (pause) pause = false;

            if (locked){
                SetTimeScale(desiredTime);
            }
            else {
                // We assume the vanilla game speed was 1f before editing it
                SetTimeScale(1f);
            }
        }

        void UpdateLockedButton()
        {
            Color color = locked ? new Color(0.3f, 0.3f, 0.2f) : new Color(0.2f, 0.2f, 0.2f);
            RuntimeHelper.SetColorBlock(lockBtn.Component, color, color * 1.2f, color * 0.7f);
            lockBtn.ButtonText.text = locked ? "Unlock" : "Lock";
        }

        // UI Construction

        void ConstructUI(GameObject parent)
        {
            Text timeLabel = UIFactory.CreateLabel(parent, "TimeLabel", "Time:", TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(timeLabel.gameObject, minHeight: 25, minWidth: 35);

            timeInput = UIFactory.CreateInputField(parent, "TimeInput", "timeScale");
            UIFactory.SetLayoutElement(timeInput.Component.gameObject, minHeight: 25, minWidth: 40);
            timeInput.Component.GetOnEndEdit().AddListener(OnTimeInputEndEdit);

            timeInput.Text = string.Empty;
            timeInput.Text = Time.timeScale.ToString();

            GameObject sliderObj = UIFactory.CreateSlider(parent, "Slider_time_scale", out slider);
            UIFactory.SetLayoutElement(sliderObj, minHeight: 25, minWidth: 75, flexibleWidth: 0);
            slider.onValueChanged.AddListener((newTimeScale) => {
                desiredTime = newTimeScale;
                timeInput.Text = desiredTime.ToString("0.00");
                
                if (shouldSliderUnpauseOnValueChange){
                    pause = false;
                    // Don't save 0 as a previous desired time, it might not do anything when unpausing
                    if (desiredTime != 0) previousDesiredTime = desiredTime;
                    previousLocked = locked;
                }
            });
            slider.m_FillImage.color = Color.clear;
            slider.value = 1;
            slider.minValue = 0f;
            slider.maxValue = 2f;

            lockBtn = UIFactory.CreateButton(parent, "PauseButton", "Lock", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(lockBtn.Component.gameObject, minHeight: 25, minWidth: 50);
            lockBtn.OnClick += OnLockedButtonClicked;
        }

        // Only allow Time.timeScale to be set if the user hasn't "locked" it or if we are setting the value internally.

        static void InitPatch()
        {

            try
            {
                MethodInfo target = typeof(Time).GetProperty("timeScale").GetSetMethod();
#if CPP
                if (IL2CPPUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(target) == null)
                    return;
#endif
                ExplorerCore.Harmony.Patch(target,
                    prefix: new(AccessTools.Method(typeof(TimeScaleWidget), nameof(Prefix_Time_set_timeScale))));
            }
            catch { }
        }

        static bool Prefix_Time_set_timeScale()
        {
            return !Instance.locked || Instance.settingTimeScale;
        }
    }
}
