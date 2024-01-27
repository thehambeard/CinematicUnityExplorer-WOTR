using UnityExplorer.UI;
using UniverseLib.Input;
#if CPP
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#else
using Il2CppInterop.Runtime.Injection;
#endif
#endif
using UnityExplorer.UI.Panels;
using UnityExplorer.UI.Widgets;
using System;


namespace UnityExplorer
{
    public class ExplorerBehaviour : MonoBehaviour
    {
        internal static ExplorerBehaviour Instance { get; private set; }

#if CPP
        public ExplorerBehaviour(System.IntPtr ptr) : base(ptr) { }
#endif

        internal static void Setup()
        {
#if CPP
            ClassInjector.RegisterTypeInIl2Cpp<ExplorerBehaviour>();
#endif

            GameObject obj = new("ExplorerBehaviour");
            DontDestroyOnLoad(obj);
            obj.hideFlags = HideFlags.HideAndDontSave;
            Instance = obj.AddComponent<ExplorerBehaviour>();
        }

        internal void Update()
        {
            ExplorerCore.Update();
        }

        // For editor, to clean up objects

        internal void OnDestroy()
        {
            OnApplicationQuit();
        }

        internal bool quitting;

        internal void OnApplicationQuit()
        {
            if (quitting) return;
            quitting = true;

            TryDestroy(UIManager.UIRoot?.transform.root.gameObject);

            TryDestroy((typeof(Universe).Assembly.GetType("UniverseLib.UniversalBehaviour")
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null, null)
                as Component).gameObject);

            TryDestroy(this.gameObject);
        }

        internal void TryDestroy(GameObject obj)
        {
            try
            {
                if (obj)
                    Destroy(obj);
            }
            catch { }
        }
    }

    // Cinematic stuff

    public class KeypressListener : MonoBehaviour
    {
        internal static KeypressListener Instance { get; private set; }

#if CPP
        public KeypressListener(System.IntPtr ptr) : base(ptr) { }
#endif
        bool frameSkip;

        internal static void Setup()
        {
#if CPP
            ClassInjector.RegisterTypeInIl2Cpp<KeypressListener>();
#endif

            GameObject obj = new("KeypressListener");
            DontDestroyOnLoad(obj);
            obj.hideFlags = HideFlags.HideAndDontSave;
            Instance = obj.AddComponent<KeypressListener>();
        }

        public void Update()
        {
            // Continous checks and actions
            stopFrameSkip();
            maybeForcePause();
            UIManager.GetPanel<UnityExplorer.UI.Panels.Misc>(UIManager.Panels.Misc).MaybeTakeScreenshot();

            if (InputManager.GetKeyDown(KeyCode.Pause))
            {
                UIManager.GetTimeScaleWidget().PauseToggle();
            }

            // FrameSkip
            if (InputManager.GetKeyDown(KeyCode.PageDown))
            {
                if (UIManager.GetTimeScaleWidget().IsPaused()) {
                    UIManager.GetTimeScaleWidget().PauseToggle();
                    frameSkip = true;
                }
            }

            if (InputManager.GetKeyDown(KeyCode.F12))
            {
                UIManager.GetPanel<UnityExplorer.UI.Panels.Misc>(UIManager.Panels.Misc).screenshotStatus = UnityExplorer.UI.Panels.Misc.ScreenshotState.TurnOffUI;
            }

            if (InputManager.GetKeyDown(KeyCode.Delete))
            {
                UIManager.GetPanel<UnityExplorer.UI.Panels.Misc>(UIManager.Panels.Misc).ToggleHUDElements();
            }

            if (InputManager.GetKeyDown(KeyCode.Insert))
            {
                FreeCamPanel.StartStopButton_OnClick();
            }

            if (InputManager.GetKeyDown(KeyCode.Home))
            {
                FreeCamPanel.blockFreecamMovementToggle.isOn = !FreeCamPanel.blockFreecamMovementToggle.isOn;
            }
        }

        void stopFrameSkip(){
            if (frameSkip && !UIManager.GetTimeScaleWidget().IsPaused()){
                frameSkip = false;
                UIManager.GetTimeScaleWidget().PauseToggle();
            }
        }

        void maybeForcePause(){
            // Force pause no matter the game timescale changes
            if (UIManager.GetTimeScaleWidget().IsPaused() && Time.timeScale != 0) {
                UIManager.GetTimeScaleWidget().SetTimeScale(0f);
            }
        }
    }
}
