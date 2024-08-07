using UnityExplorer.CacheObject;
using UnityExplorer.ObjectExplorer;
using UnityExplorer.Inspectors;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;
using UniverseLib.UI.Widgets.ScrollView;
using UnityEngine;
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#endif
#if INTEROP
using Il2CppInterop.Runtime.Injection;
#endif

namespace UnityExplorer.UI.Panels
{
    public class AnimatorPanel : UEPanel, ICellPoolDataSource<AnimatorCell>
    {
        public AnimatorPanel(UIBase owner) : base(owner)
        {
        }

        public override string Name => "Animator";
        public override UIManager.Panels PanelType => UIManager.Panels.AnimatorPanel;
        public override int MinWidth => 1300;
        public override int MinHeight => 200;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;

        AnimatorPausePlayButton masterAnimatorPlayer;
        Toggle masterMeshToggle;

        private static ScrollPool<AnimatorCell> animatorScrollPool;
        internal List<AnimatorPlayer> animators = new List<AnimatorPlayer>();
        public int ItemCount => animators.Count;
        private static bool DoneScrollPoolInit;

        public override void SetActive(bool active)
        {
            base.SetActive(active);

            if (active && !DoneScrollPoolInit)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(this.Rect);
                animatorScrollPool.Initialize(this);
                DoneScrollPoolInit = true;
            }
        }

        private void FindAllAnimators(){
            // Enable all animators on refresh
            masterAnimatorPlayer.isOn = true; // Will also trigger "MasterToggleAnimators(true)"

            Type searchType = ReflectionUtility.GetTypeByName("UnityEngine.Animator");
            searchType = searchType is Type type ? type : searchType.GetActualType();
            List<AnimatorPlayer> newAnimators = RuntimeHelper.FindObjectsOfTypeAll(searchType).Select(obj => obj.TryCast<Behaviour>())
            .Where(a => a.isActiveAndEnabled && (a.GetComponentsInChildren<Rigidbody>(false).Length != 0 || a.GetComponentsInChildren<SkinnedMeshRenderer>(false).Length != 0))
            .OrderBy(x=>x.name)
            .Select(a => new AnimatorPlayer(a))
            .ToList();

            // If there are old animators in the new list keep the old object with its properties.
            for(int i = 0; i < animators.Count; i++)
            {
                if (animators[i].animator.wrappedObject != null){
                    int newAnimatorsIndex = newAnimators.FindIndex(a => a.animator.wrappedObject == animators[i].animator.wrappedObject);
                    if (newAnimatorsIndex != -1) {
                        // If refreshing the animator gives us new animations, add them to the already existing ones.
                        // Might break stuff.
                        foreach (IAnimationClip animationClip in newAnimators[newAnimatorsIndex].animations) {
                            // TODO: Refactor AnimatorPlayer.animations from List<IAnimationClip> to HashSet<IAnimationClip> to avoid checking this
                            if (!animators[i].animations.Contains(animationClip))
                                animators[i].animations.Add(animationClip);
                        }
                        newAnimators[newAnimatorsIndex] = animators[i];

                        // Reset meshes
                        newAnimators[newAnimatorsIndex].SearchMeshes();
                        newAnimators[newAnimatorsIndex].MaybeResetBonesPanel();
                    }
                }
            }
            animators = newAnimators;

            animatorScrollPool.Refresh(true, false);
        }

        
        private void ResetAllAnimators(){
            foreach (AnimatorPlayer animatorPlayer in animators)
            {
                animatorPlayer.ResetAnimation();
            }
        }

        public void MasterToggleAnimators(){
            bool enable = masterAnimatorPlayer.isOn;

            // Load animators for the first time if there are not any
            if (animators.Count == 0) FindAllAnimators();

            foreach (AnimatorPlayer animatorPlayer in animators){
                if (!animatorPlayer.shouldIgnoreMasterToggle){
                    if (animatorPlayer.animator.wrappedObject != null)
                        animatorPlayer.animator.speed = enable ? 1 : 0;
                }
            }

            animatorScrollPool.Refresh(true, false);
        }

        public void MasterToggleMeshes(bool enable){
            // Load animators for the first time if there are not any
            if (animators.Count == 0) FindAllAnimators();

            foreach (AnimatorPlayer animatorPlayer in animators){
                if (!animatorPlayer.shouldIgnoreMasterToggle){
                    animatorPlayer.SetMeshesEnabled(enable);
                }
            }

            animatorScrollPool.Refresh(true, false);
        }

        public void HotkeyToggleAnimators(){
            masterAnimatorPlayer.isOn = !masterAnimatorPlayer.isOn;
        }

        // ~~~~~~~~ UI construction / callbacks ~~~~~~~~

        protected override void ConstructPanelContent()
        {
            GameObject firstGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "MainOptions", false, false, true, true, 3,
                default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(firstGroup, minHeight: 25, flexibleWidth: 9999);

            GameObject headerSpace1 = UIFactory.CreateUIObject("HeaderSpace1", firstGroup);
            UIFactory.SetLayoutElement(headerSpace1, minWidth: 0, flexibleWidth: 0);

            GameObject meshObj = UIFactory.CreateToggle(firstGroup, "Master Mesh Toggle", out masterMeshToggle, out Text masterMeshText);
            UIFactory.SetLayoutElement(meshObj, minHeight: 25, minWidth: 230);
            masterMeshToggle.onValueChanged.AddListener(value => MasterToggleMeshes(value));
            masterMeshText.text = "Master Mesh Toggler";

            masterAnimatorPlayer = new AnimatorPausePlayButton(firstGroup);
            masterAnimatorPlayer.OnClick += MasterToggleAnimators;

            Text masterAnimatorPlayerText = UIFactory.CreateLabel(firstGroup, "MasterAnimatorToggleLabel", "Master Animator Toggler", TextAnchor.MiddleRight);
            UIFactory.SetLayoutElement(masterAnimatorPlayerText.gameObject, flexibleWidth: 0, minHeight: 25);

            GameObject headerSpace2 = UIFactory.CreateUIObject("HeaderSpace2", firstGroup);
            UIFactory.SetLayoutElement(headerSpace2, minWidth: 10, flexibleWidth: 0);

            ButtonRef resetAnimators = UIFactory.CreateButton(firstGroup, "ResetAnimators", "Reset Animators");
            UIFactory.SetLayoutElement(resetAnimators.GameObject, minWidth: 150, minHeight: 25);
            resetAnimators.OnClick += ResetAllAnimators;

            GameObject secondGroup = UIFactory.CreateHorizontalGroup(firstGroup, "HeaderRight", false, false, true, true, 3,
            default, new Color(1, 1, 1, 0), TextAnchor.MiddleRight);
            UIFactory.SetLayoutElement(secondGroup, minHeight: 25, flexibleWidth: 9999);

            ButtonRef updateAnimators = UIFactory.CreateButton(secondGroup, "RefreshAnimators", "Refresh Animators");
            UIFactory.SetLayoutElement(updateAnimators.GameObject, minWidth: 150, minHeight: 25);
            updateAnimators.OnClick += FindAllAnimators;

            GameObject headerSpaceRight = UIFactory.CreateUIObject("HeaderSpaceRight", firstGroup);
            UIFactory.SetLayoutElement(headerSpaceRight, minWidth: 25, flexibleWidth: 0);

            animatorScrollPool = UIFactory.CreateScrollPool<AnimatorCell>(ContentRoot, "AnimatorsList", out GameObject scrollObj,
                out GameObject scrollContent, new Color(0.03f, 0.03f, 0.03f));
            UIFactory.SetLayoutElement(scrollObj, flexibleWidth: 9999, flexibleHeight: 9999);
        }

        public void SetCell(AnimatorCell cell, int index)
        {
            if (index >= animators.Count)
            {
                cell.Disable();
                return;
            }

            AnimatorPlayer animatorPlayer = animators[index];

            if (animatorPlayer.animator.wrappedObject == null)
                return;
            // Check if the animator wrapped object was deleted by trying to access one of its properties
            try {
                string check = animatorPlayer.animator.name;
            }
            catch {
                FindAllAnimators();
                return;
            }

            cell.SetAnimatorPlayer(animatorPlayer);
        }

        public void OnCellBorrowed(AnimatorCell cell) { }
    }

    // ButtonRef wrapper to act as a toggle with clearer UI
    public class AnimatorPausePlayButton
    {
        private ButtonRef innerButton;
        public Button Component => innerButton.Component;
        public GameObject GameObject => innerButton.GameObject;
        private bool isPlaying;

        public AnimatorPausePlayButton(GameObject ui, bool state = true)
        {
            innerButton = UIFactory.CreateButton(ui, "InnerAnimatorPlayButton", "");
            UIFactory.SetLayoutElement(innerButton.GameObject, minHeight: 25, minWidth: 25);
            innerButton.OnClick += SetToggleButtonState;
            isPlaying = state;
            UpdateButton();
        }

        void OnPlay(){
            innerButton.ButtonText.text = "❚❚";
            RuntimeHelper.SetColorBlockAuto(innerButton.Component, new(0.4f, 0.2f, 0.2f));
        }

        void OnPause(){
            innerButton.ButtonText.text = "►";
            RuntimeHelper.SetColorBlockAuto(innerButton.Component, new(0.2f, 0.4f, 0.2f));
        }

        void UpdateButton(){
            if (isPlaying)
            {
                OnPlay();
            }
            else
            {
                OnPause();
            }
        }

        void SetToggleButtonState()
        {
            isPlaying = !isPlaying;
            UpdateButton();
        }

        public bool isOn
        {
            get {
                return isPlaying;
            }
            set {
                if (value != isPlaying){
                    SetToggleButtonState();
                }
            }
        }

        public Action OnClick
        {
            get {
                return innerButton.OnClick;
            }
            set {
                innerButton.OnClick = value;
            }
        }
    }
}
