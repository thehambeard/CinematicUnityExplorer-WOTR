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
        public AnimatorPausePlayButton animatorToggler;
        public Toggle MeshToggle;
        public ButtonRef inspectButton;
        public Dropdown animatorDropdown;
        ButtonRef favAnimation;
        Slider animationTimeline;
        ButtonRef playButton;
        ButtonRef openBonesPanelButton;

        // ICell
        public float DefaultHeight => 30f;
        public GameObject UIRoot { get; set; }
        public RectTransform Rect { get; set; }

        public bool Enabled => UIRoot.activeSelf;
        public void Enable() => UIRoot.SetActive(true);
        public void Disable() => UIRoot.SetActive(false);

        public void SetAnimatorPlayer(AnimatorPlayer animatorPlayer){
            this.animatorPlayer = animatorPlayer;
            inspectButton.ButtonText.text = animatorPlayer.animator.name;
            IgnoreMasterToggle.isOn = animatorPlayer.shouldIgnoreMasterToggle;
            animatorToggler.isOn = animatorPlayer.animator.speed != 0;
            MeshToggle.isOn = animatorPlayer.IsMeshHidden();

            UpdateDropdownOptions();
        }

        private void UpdateDropdownOptions(){
            if (animatorPlayer.animator.runtimeAnimatorController != null){
                animatorDropdown.options.Clear();

                // For some reason, the favourite animations list was'nt being ordered when adding a new animation
                foreach (IAnimationClip animation in animatorPlayer.favAnimations.OrderBy(x => x.name)){
                    animatorDropdown.options.Add(new Dropdown.OptionData(animation.name));
                }

                foreach (IAnimationClip animation in animatorPlayer.animations){
                    if (!animatorPlayer.favAnimations.Contains(animation)){
                        animatorDropdown.options.Add(new Dropdown.OptionData(animation.name));
                    }
                }

                animatorDropdown.captionText.text = animatorPlayer.overridingAnimation.name;
            }
        }

        private void PlayButton_OnClick(){
            animatorPlayer.PlayOverridingAnimation(0);
            // Needed for the new animation to play for some reason
            EnableAnimation(false);
            EnableAnimation(true);
        }

        public virtual GameObject CreateContent(GameObject parent)
        {
            UIRoot = UIFactory.CreateUIObject("AnimatorCell", parent);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(UIRoot, false, false, true, true, 4, childAlignment: TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(UIRoot, minHeight: 25, flexibleWidth: 9999);

            GameObject MeshToggleObj = UIFactory.CreateToggle(UIRoot, "MeshToggle", out MeshToggle, out Text MeshToggleText);
            UIFactory.SetLayoutElement(MeshToggleObj, minHeight: 30);
            MeshToggle.onValueChanged.AddListener(EnableMesh);

            Rect = UIRoot.GetComponent<RectTransform>();
            Rect.anchorMin = new Vector2(0, 1);
            Rect.anchorMax = new Vector2(0, 1);
            Rect.pivot = new Vector2(0.5f, 1);
            Rect.sizeDelta = new Vector2(30, 30);
            
            UIFactory.SetLayoutElement(UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);

            inspectButton = UIFactory.CreateButton(UIRoot, "InspectButton", "");
            UIFactory.SetLayoutElement(inspectButton.GameObject, minWidth: 200, minHeight: 25);
            inspectButton.OnClick += () => InspectorManager.Inspect(animatorPlayer.animator.gameObject);

            animatorToggler = new AnimatorPausePlayButton(UIRoot, animatorPlayer != null && animatorPlayer.animator.speed == 1);
            animatorToggler.OnClick += ButtonEnableAnimation;

            ButtonRef resetAnimation = UIFactory.CreateButton(UIRoot, "Reset Animation", "Reset");
            UIFactory.SetLayoutElement(resetAnimation.GameObject, minWidth: 50, minHeight: 25);
            resetAnimation.OnClick += ResetAnimation;

            GameObject ignoresMasterTogglerObj = UIFactory.CreateToggle(UIRoot, "AnimatorIgnoreMasterToggle", out IgnoreMasterToggle, out Text ignoreMasterToggleText);
            UIFactory.SetLayoutElement(ignoresMasterTogglerObj, minHeight: 25, minWidth: 155);
            IgnoreMasterToggle.isOn = false;
            IgnoreMasterToggle.onValueChanged.AddListener(IgnoreMasterToggle_Clicked);
            ignoreMasterToggleText.text = "Ignore Master Toggle  ";

            ButtonRef prevAnimation = UIFactory.CreateButton(UIRoot, "PreviousAnimation", "◀", new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(prevAnimation.Component.gameObject, minHeight: 25, minWidth: 25);
            prevAnimation.OnClick += () => {
                if (animatorPlayer.animator.wrappedObject == null || animatorDropdown == null)
                    return;
                animatorDropdown.value = animatorDropdown.value == 0 ? animatorDropdown.options.Count - 1 : animatorDropdown.value - 1;

                favAnimation.ButtonText.text = animatorPlayer.IsAnimationFaved(animatorPlayer.overridingAnimation) ? "★" : "☆";
            };

            GameObject overridingAnimationObj = UIFactory.CreateDropdown(UIRoot, "Animations_Dropdown", out animatorDropdown, null, 14, (idx) => {
                if (animatorPlayer.animator.wrappedObject == null)
                    return;
                animatorPlayer.overridingAnimation = idx < animatorDropdown.options.Count ? animatorPlayer.animations.Find(a => a.name == animatorDropdown.options[idx].text) : animatorPlayer.overridingAnimation;

                favAnimation.ButtonText.text = animatorPlayer.IsAnimationFaved(animatorPlayer.overridingAnimation) ? "★": "☆";
                }
            );
            
            UIFactory.SetLayoutElement(overridingAnimationObj, minHeight: 25, minWidth: 200, flexibleWidth: 9999);

            ButtonRef nextAnimation = UIFactory.CreateButton(UIRoot, "NextAnimation", "▶", new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(nextAnimation.Component.gameObject, minHeight: 25, minWidth: 25);
            nextAnimation.OnClick += () => {
                if (animatorPlayer.animator.wrappedObject == null || animatorDropdown == null)
                    return;
                animatorDropdown.value = animatorDropdown.value == animatorDropdown.options.Count - 1 ? 0 : animatorDropdown.value + 1;

                favAnimation.ButtonText.text = animatorPlayer.IsAnimationFaved(animatorPlayer.overridingAnimation) ? "★" : "☆";
            };

            favAnimation = UIFactory.CreateButton(UIRoot, "FavAnimation", "☆", new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(favAnimation.Component.gameObject, minHeight: 25, minWidth: 25);
            favAnimation.OnClick += () => {
                if (animatorPlayer.animator.wrappedObject == null || animatorDropdown == null)
                    return;

                bool isAnimationFaved = animatorPlayer.IsAnimationFaved(animatorPlayer.overridingAnimation);
                if (!isAnimationFaved){
                    animatorPlayer.FavAnimation(animatorPlayer.overridingAnimation);
                }
                else {
                    animatorPlayer.UnfavAnimation(animatorPlayer.overridingAnimation);
                }

                favAnimation.ButtonText.text = !isAnimationFaved ? "★" : "☆";
                //UpdateDropdownOptions();
            };

            GameObject animationTimelineObj = UIFactory.CreateSlider(UIRoot, "AnimationTimelineSlider", out animationTimeline);
            UIFactory.SetLayoutElement(animationTimelineObj, minHeight: 25, minWidth: 200, flexibleHeight: 0);
            animationTimeline.minValue = 0;
            animationTimeline.maxValue = 1;
            animationTimeline.onValueChanged.AddListener((float val) => {
                animatorPlayer.PlayOverridingAnimation(val);
                animatorToggler.isOn = false;
            });

            playButton = UIFactory.CreateButton(UIRoot, "PlayButton", "Play", new Color(0.2f, 0.26f, 0.2f));
            UIFactory.SetLayoutElement(playButton.Component.gameObject, minHeight: 25, minWidth: 90);
            playButton.OnClick += PlayButton_OnClick;

            openBonesPanelButton = UIFactory.CreateButton(UIRoot, "OpenBonesPanelButton", "Open Bones Panel");
            UIFactory.SetLayoutElement(openBonesPanelButton.Component.gameObject, minWidth: 150, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);

            openBonesPanelButton.OnClick += () => { animatorPlayer.OpenBonesPanel(); };

            return UIRoot;
        }

        public void ResetAnimation(){
            animatorPlayer.ResetAnimation();
        }

        internal void IgnoreMasterToggle_Clicked(bool value){
            animatorPlayer.shouldIgnoreMasterToggle = value;
        }

        internal void ButtonEnableAnimation(){
            EnableAnimation(animatorToggler.isOn);
        }

        internal void EnableAnimation(bool value){
            if (animatorPlayer.animator.wrappedObject != null)
                animatorPlayer.animator.speed = value ? 1 : 0;
        }

        internal void EnableMesh(bool value){
            animatorPlayer.SetMeshesEnabled(value);
        }
    }
}
