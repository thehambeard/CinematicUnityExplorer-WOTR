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
        public override int MinWidth => 900;
        public override int MinHeight => 200;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;

        Toggle masterAnimatorToggle = new Toggle();

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

            animatorScrollPool.Refresh(true, false);
        }

        private void FindAllAnimators(){
            // Enable all animators on refresh
            masterAnimatorToggle.isOn = true; // Will also trigger "MasterToggleAnimators(true)"

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
                    if (newAnimatorsIndex != -1)
                        newAnimators[newAnimatorsIndex] = animators[i];
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

        public void MasterToggleAnimators(bool enable){
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

        public void HotkeyToggleAnimators(){
            masterAnimatorToggle.isOn = !masterAnimatorToggle.isOn;
        }

        // ~~~~~~~~ UI construction / callbacks ~~~~~~~~

        protected override void ConstructPanelContent()
        {
            GameObject firstGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "MainOptions", false, false, true, true, 3,
                default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(firstGroup, minHeight: 25, flexibleWidth: 9999);
            //UIElements.Add(horiGroup);

            ButtonRef updateAnimators = UIFactory.CreateButton(firstGroup, "RefreshAnimators", "Refresh Animators");
            UIFactory.SetLayoutElement(updateAnimators.GameObject, minWidth: 150, minHeight: 25);
            updateAnimators.OnClick += FindAllAnimators;

            ButtonRef resetAnimators = UIFactory.CreateButton(firstGroup, "ResetAnimators", "Reset Animators");
            UIFactory.SetLayoutElement(resetAnimators.GameObject, minWidth: 150, minHeight: 25);
            resetAnimators.OnClick += ResetAllAnimators;

            GameObject animatorObj = UIFactory.CreateToggle(firstGroup, $"Master Animation Toggle", out masterAnimatorToggle, out Text masterAnimatorText);
            UIFactory.SetLayoutElement(animatorObj, minHeight: 25);
            masterAnimatorToggle.isOn = true;
            masterAnimatorToggle.onValueChanged.AddListener(value => MasterToggleAnimators(value));
            masterAnimatorText.text = "Master Toggler";

            GameObject secondGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "MainOptions", false, false, true, true, 3,
            default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(firstGroup, minHeight: 25, flexibleWidth: 9999);

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
}
