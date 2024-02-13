using UnityEngine;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

#if CPP
using Animator = UnityEngine.Behaviour;
#endif

namespace UnityExplorer.UI.Panels
{
    public class AnimatorCell : ICell
    {
        private bool autoIgnoreMasterToggleSet;
        public Toggle IgnoreMasterToggle;
        public Toggle AnimatorToggle;

        public Text name;
        public Animator animator;

        // ICell
        public float DefaultHeight => 25f;
        public GameObject UIRoot { get; set; }
        public RectTransform Rect { get; set; }

        public bool Enabled => UIRoot.activeSelf;
        public void Enable() => UIRoot.SetActive(true);
        public void Disable() => UIRoot.SetActive(false);

// IL2CPP games seem to have animation-related code stripped from their builds
#if MONO
        ButtonRef playButton;
        private bool manuallyPlayedAnimation;
        private bool skippedStopFrames;

        private AnimationClip currentAnimation;
        private AnimationClip defaultAnimation;

        public void DrawAnimatorPlayer(){
            if (playButton == null){
                List<AnimationClip> animations = animator.runtimeAnimatorController.animationClips.OrderBy(x=>x.name).Where(c => c.length > 0).Distinct().ToList();

                //ExplorerCore.LogWarning(animations.Count);
                AnimatorClipInfo[] playingAnimations = animator.GetCurrentAnimatorClipInfo(0);
                currentAnimation = playingAnimations.Count() != 0 ? playingAnimations[0].clip : animations[0];

                GameObject currentAnimationObj = UIFactory.CreateDropdown(UIRoot, $"Animations_{name}", out Dropdown dropdown, null, 14, (idx) => currentAnimation = animations[idx]);
                UIFactory.SetLayoutElement(currentAnimationObj, minHeight: 25, minWidth: 100);
                foreach (AnimationClip animation in animations)
                    dropdown.options.Add(new Dropdown.OptionData(animation.name));

                playButton = UIFactory.CreateButton(UIRoot, "PlayButton", "Play", new Color(0.2f, 0.26f, 0.2f));
                UIFactory.SetLayoutElement(playButton.Component.gameObject, minHeight: 5, minWidth: 90);
                playButton.OnClick += PlayButton_OnClick;
            }
        }

        private void PlayButton_OnClick(){
            // We save the last animation played by the game in case we want to go back to it
            if (defaultAnimation == null){
                AnimatorClipInfo[] playingAnimations = animator.GetCurrentAnimatorClipInfo(0);
                defaultAnimation = playingAnimations.Count() != 0 ? playingAnimations[0].clip : null;
            }

            skippedStopFrames = false;

            AnimatorToggle.isOn = true;
            manuallyPlayedAnimation = true;
            animator.Play(currentAnimation.name);
        }

        // Disables the animator when the animation we manually triggered isn't present on the subject anymore
        public bool IsPlayingSelectedAnimation(){
            if (animator != null && currentAnimation != null && name.text == "Player"){
                if (manuallyPlayedAnimation && !GetAllCurrentAnimations().Contains(currentAnimation)){

                    if (!skippedStopFrames){
                        skippedStopFrames = true;
                        return false;
                    }

                    manuallyPlayedAnimation = false;
                    AnimatorToggle.isOn = false;
                    return true;
                }
            }
            return false;
        }

        private List<AnimationClip> GetAllCurrentAnimations(){
            List<AnimationClip> allAnimations = new List<AnimationClip>();
            for (int layer = 0; layer < animator.layerCount; layer++){
                allAnimations.AddRange(animator.GetCurrentAnimatorClipInfo(layer).Select(ainfo => ainfo.clip).ToList());
            }
            return allAnimations;
        }

        public void ResetAnimation(){
            if (defaultAnimation != null){
                manuallyPlayedAnimation = false;

                animator.Play(defaultAnimation.name);
                AnimatorToggle.isOn = true;
                defaultAnimation = null;
            }
        }
#endif

        // If it's the first time we are rendering the AnimatorCell, assign the ignore value automatically based on it's name
        public void MaybeSetIgnoreMasterToggleSet(){
            if (animator != null && !autoIgnoreMasterToggleSet){
                // A weird canse insensitive "Contains" to identify the player
                IgnoreMasterToggle.isOn = animator.gameObject.name.IndexOf("play", 0, StringComparison.OrdinalIgnoreCase) >= 0;
                autoIgnoreMasterToggleSet = true;
            }
        }

        public virtual GameObject CreateContent(GameObject parent)
        {
            GameObject AnimatorToggleObj = UIFactory.CreateToggle(parent, $"AnimatorToggle", out AnimatorToggle, out Text animatorToggleText);
            UIFactory.SetLayoutElement(AnimatorToggleObj, minHeight: 25);
            AnimatorToggle.isOn = true;
            AnimatorToggle.onValueChanged.AddListener(value => animator.enabled = value);

            UIRoot = AnimatorToggleObj;
            UIRoot.SetActive(false);

            Rect = UIRoot.GetComponent<RectTransform>();
            Rect.anchorMin = new Vector2(0, 1);
            Rect.anchorMax = new Vector2(0, 1);
            Rect.pivot = new Vector2(0.5f, 1);
            Rect.sizeDelta = new Vector2(25, 25);
            
            UIFactory.SetLayoutElement(UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 0);

            name = UIFactory.CreateLabel(UIRoot, "NameLabel", "", TextAnchor.MiddleLeft, Color.white, false, 14);
            UIFactory.SetLayoutElement(name.gameObject, minHeight: 25, minWidth: 30, flexibleWidth: 40);

#if MONO
            ButtonRef resetAnimation = UIFactory.CreateButton(UIRoot, "Reset Animation", "Reset");
            UIFactory.SetLayoutElement(resetAnimation.GameObject, minWidth: 50, minHeight: 25);
            resetAnimation.OnClick += ResetAnimation;
#endif

            GameObject ignoresMasterTogglerObj = UIFactory.CreateToggle(UIRoot, $"AnimatorIgnoreMasterToggle", out IgnoreMasterToggle, out Text ignoreMasterToggleText);
            UIFactory.SetLayoutElement(ignoresMasterTogglerObj, minHeight: 25);
            IgnoreMasterToggle.isOn = false;
            ignoreMasterToggleText.text = "Ignore Master Toggle  ";

            return UIRoot;
        }
    }
}
