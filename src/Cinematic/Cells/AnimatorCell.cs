using UnityEngine;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

#if CPP
#if INTEROP
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;
#else
using UnhollowerRuntimeLib;
using UnhollowerBaseLib;
#endif
#endif

namespace UnityExplorer.UI.Panels
{
    public class AnimatorCell : ICell
    {
        public AnimatorPlayer animatorPlayer;

        public Toggle IgnoreMasterToggle;
        public Toggle AnimatorToggle;
        public ButtonRef inspectButton;
        ButtonRef playButton;
        public Dropdown animatorDropdown;

        // ICell
        public float DefaultHeight => 25f;
        public GameObject UIRoot { get; set; }
        public RectTransform Rect { get; set; }

        public bool Enabled => UIRoot.activeSelf;
        public void Enable() => UIRoot.SetActive(true);
        public void Disable() => UIRoot.SetActive(false);

        public void SetAnimatorPlayer(AnimatorPlayer animatorPlayer){
            this.animatorPlayer = animatorPlayer;
            inspectButton.ButtonText.text = animatorPlayer.animator.name;
            IgnoreMasterToggle.isOn = animatorPlayer.shouldIgnoreMasterToggle;
            AnimatorToggle.isOn = animatorPlayer.animator.speed != 0;

            // Updated dropdown options
            if (animatorPlayer.animator.runtimeAnimatorController != null){
                animatorDropdown.options.Clear();
                foreach (IAnimationClip animation in animatorPlayer.animations)
                    animatorDropdown.options.Add(new Dropdown.OptionData(animation.name));

                animatorDropdown.value = Math.Max(0, animatorPlayer.animations.FindIndex(a => a.name == animatorPlayer.overridingAnimation.name));
                if (animatorDropdown.value == 0) animatorDropdown.captionText.text = animatorPlayer.animations[0].name;
            }
        }

        private void PlayButton_OnClick(){
            animatorPlayer.PlayOverridingAnimation();
            // Turn the toggle on to play the animation
            if (!AnimatorToggle.isOn) AnimatorToggle.isOn = true;
        }

        public virtual GameObject CreateContent(GameObject parent)
        {
            GameObject AnimatorToggleObj = UIFactory.CreateToggle(parent, $"AnimatorToggle", out AnimatorToggle, out Text animatorToggleText);
            UIFactory.SetLayoutElement(AnimatorToggleObj, minHeight: 25);
            AnimatorToggle.onValueChanged.AddListener(EnableAnimation);

            UIRoot = AnimatorToggleObj;
            UIRoot.SetActive(false);

            Rect = UIRoot.GetComponent<RectTransform>();
            Rect.anchorMin = new Vector2(0, 1);
            Rect.anchorMax = new Vector2(0, 1);
            Rect.pivot = new Vector2(0.5f, 1);
            Rect.sizeDelta = new Vector2(25, 25);
            
            UIFactory.SetLayoutElement(UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 0);

            inspectButton = UIFactory.CreateButton(UIRoot, "InspectButton", "");
            UIFactory.SetLayoutElement(inspectButton.GameObject, minWidth: 250, minHeight: 25);
            inspectButton.OnClick += () => InspectorManager.Inspect(animatorPlayer.animator.gameObject);

            ButtonRef resetAnimation = UIFactory.CreateButton(UIRoot, "Reset Animation", "Reset");
            UIFactory.SetLayoutElement(resetAnimation.GameObject, minWidth: 50, minHeight: 25);
            resetAnimation.OnClick += ResetAnimation;

            GameObject ignoresMasterTogglerObj = UIFactory.CreateToggle(UIRoot, $"AnimatorIgnoreMasterToggle", out IgnoreMasterToggle, out Text ignoreMasterToggleText);
            UIFactory.SetLayoutElement(ignoresMasterTogglerObj, minHeight: 25, minWidth: 200);
            IgnoreMasterToggle.isOn = false;
            IgnoreMasterToggle.onValueChanged.AddListener(IgnoreMasterToggle_Clicked);
            ignoreMasterToggleText.text = "Ignore Master Toggle  ";

            ButtonRef prevAnimation = UIFactory.CreateButton(UIRoot, "PreviousAnimation", "◀", new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(prevAnimation.Component.gameObject, minHeight: 25, minWidth: 25);
            prevAnimation.OnClick += () => {
                if (animatorPlayer.animator.wrappedObject == null || animatorDropdown == null)
                    return;
                animatorDropdown.value = animatorDropdown.value == 0 ? animatorDropdown.options.Count - 1 : animatorDropdown.value - 1;
            };

            GameObject overridingAnimationObj = UIFactory.CreateDropdown(UIRoot, $"Animations_Dropdown", out animatorDropdown, null, 14, (idx) => {
                if (animatorPlayer.animator.wrappedObject == null)
                    return;
                animatorPlayer.overridingAnimation = idx < animatorPlayer.animations.Count() ? animatorPlayer.animations[idx] : animatorPlayer.overridingAnimation;
                }
            );
            
            UIFactory.SetLayoutElement(overridingAnimationObj, minHeight: 25, minWidth: 200, flexibleWidth: 9999);

            ButtonRef nextAnimation = UIFactory.CreateButton(UIRoot, "NextAnimation", "▶", new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(nextAnimation.Component.gameObject, minHeight: 25, minWidth: 25);
            nextAnimation.OnClick += () => {
                if (animatorPlayer.animator.wrappedObject == null || animatorDropdown == null)
                    return;
                animatorDropdown.value = animatorDropdown.value == animatorDropdown.options.Count - 1 ? 0 : animatorDropdown.value + 1;
            };

            playButton = UIFactory.CreateButton(UIRoot, "PlayButton", "Play", new Color(0.2f, 0.26f, 0.2f));
            UIFactory.SetLayoutElement(playButton.Component.gameObject, minHeight: 25, minWidth: 90);
            playButton.OnClick += PlayButton_OnClick;

            return UIRoot;
        }

        public void ResetAnimation(){
            animatorPlayer.ResetAnimation();
        }

        internal void IgnoreMasterToggle_Clicked(bool value){
            animatorPlayer.shouldIgnoreMasterToggle = value;
        }

        internal void EnableAnimation(bool value){
            if (animatorPlayer.animator.wrappedObject != null)
                animatorPlayer.animator.speed = value ? 1 : 0;
        }
    }
}
