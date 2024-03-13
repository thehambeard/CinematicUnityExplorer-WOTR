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
        ButtonRef favAnimation;

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
            animatorPlayer.PlayOverridingAnimation();
            // Needed for the new animation to play for some reason
            EnableAnimation(false);
            EnableAnimation(true);
        }

        public virtual GameObject CreateContent(GameObject parent)
        {
            GameObject AnimatorToggleObj = UIFactory.CreateToggle(parent, $"AnimatorToggle", out AnimatorToggle, out Text animatorToggleText);
            UIFactory.SetLayoutElement(AnimatorToggleObj, minHeight: 25);
            AnimatorToggle.isOn = animatorPlayer != null && animatorPlayer.animator.speed == 1;
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
            UIFactory.SetLayoutElement(inspectButton.GameObject, minWidth: 200, minHeight: 25);
            inspectButton.OnClick += () => InspectorManager.Inspect(animatorPlayer.animator.gameObject);

            ButtonRef resetAnimation = UIFactory.CreateButton(UIRoot, "Reset Animation", "Reset");
            UIFactory.SetLayoutElement(resetAnimation.GameObject, minWidth: 50, minHeight: 25);
            resetAnimation.OnClick += ResetAnimation;

            GameObject ignoresMasterTogglerObj = UIFactory.CreateToggle(UIRoot, $"AnimatorIgnoreMasterToggle", out IgnoreMasterToggle, out Text ignoreMasterToggleText);
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

            GameObject overridingAnimationObj = UIFactory.CreateDropdown(UIRoot, $"Animations_Dropdown", out animatorDropdown, null, 14, (idx) => {
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
