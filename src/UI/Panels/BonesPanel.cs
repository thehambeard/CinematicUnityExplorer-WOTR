using UniverseLib.UI;
using UniverseLib.UI.Panels;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.UI.Panels
{
    public class BonesManager : PanelBase, ICellPoolDataSource<BonesCell>
    {
        public override string Name => $"Bones Manager";
        public override int MinWidth => 1000;
        public override int MinHeight => 400;
        public override Vector2 DefaultAnchorMin => Vector2.zero;
        public override Vector2 DefaultAnchorMax => Vector2.zero;
        public Toggle turnOffAnimatorToggle;
        
        private IAnimator animator;
        private Text skeletonName;
        private List<Transform> bones = new List<Transform>();
        private Dictionary<string, CachedBonesTransform> bonesOriginalState = new();

        private ScrollPool<BonesCell> boneScrollPool;
        public int ItemCount => bones.Count;
        private bool DoneScrollPoolInit;

        public BonesManager(UIBase owner, List<Transform> bones, IAnimator animator) : base(owner)
        {
            this.bones = bones;
            this.animator = animator;
            skeletonName.text = $"Skeleton: {animator?.name}";
        }

        public override void SetActive(bool active)
        {
            base.SetActive(active);

            if (active && !DoneScrollPoolInit)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(this.Rect);
                boneScrollPool.Initialize(this);
                DoneScrollPoolInit = true;
            }
        }

        protected override void ConstructPanelContent()
        {
            skeletonName = UIFactory.CreateLabel(ContentRoot, $"SkeletonName", "");
            UIFactory.SetLayoutElement(skeletonName.gameObject, minWidth: 100, minHeight: 25);
            skeletonName.fontSize = 16;

            GameObject turnOffAnimatorToggleObj = UIFactory.CreateToggle(ContentRoot, "Animator toggle", out turnOffAnimatorToggle, out Text turnOffAnimatorToggleText);
            UIFactory.SetLayoutElement(turnOffAnimatorToggleObj, minHeight: 25, flexibleWidth: 9999);
            turnOffAnimatorToggle.onValueChanged.AddListener(OnTurnOffAnimatorToggle);
            turnOffAnimatorToggleText.text = "Toggle animator (needs to be off to move bones)";

            boneScrollPool = UIFactory.CreateScrollPool<BonesCell>(ContentRoot, "BonesList", out GameObject scrollObj,
                out GameObject scrollContent, new Color(0.06f, 0.06f, 0.06f));
            UIFactory.SetLayoutElement(scrollObj, flexibleWidth: 9999, flexibleHeight: 9999);
        }

        private void OnTurnOffAnimatorToggle(bool value)
        {
            if (value){
                // Restore meshes manually in case some are not part of a skeleton and won't get restored automatically.
                // Besides, this restores the scale, which the animator doesn't.
                foreach (Transform bone in bones){
                    CachedBonesTransform CachedBonesTransform = bonesOriginalState[bone.name];
                    bone.localPosition = CachedBonesTransform.position;
                    bone.localEulerAngles = CachedBonesTransform.angles;
                    bone.localScale = CachedBonesTransform.scale;
                    // We assume these were on before. If not we should save its state beforehand.
                    bone.gameObject.SetActive(true);
                }
            } else {
                bonesOriginalState.Clear();
                foreach (Transform bone in bones){
                    bonesOriginalState[bone.name] = new CachedBonesTransform(bone.localPosition, bone.localEulerAngles, bone.localScale);
                }
            }
            animator.enabled = value;
        }

        public void RestoreBoneState(string boneName)
        {
            foreach (Transform bone in bones){
                if (bone.name == boneName){
                    CachedBonesTransform CachedBonesTransform = bonesOriginalState[boneName];
                    bone.localPosition = CachedBonesTransform.position;
                    bone.localEulerAngles = CachedBonesTransform.angles;
                    bone.localScale = CachedBonesTransform.scale;
                    return;
                }
            }
        }

        public void SetCell(BonesCell cell, int index)
        {
            if (index >= bones.Count)
            {
                cell.Disable();
                return;
            }

            Transform bone = bones[index];
            cell.SetBone(bone, this);
            cell.UpdateTransformControlValues(true);
        }

        public void OnCellBorrowed(BonesCell cell) {
            cell.UpdateVectorSlider();
        }

        public override void Update()
        {
            base.Update();

            foreach(BonesCell boneCell in boneScrollPool.CellPool) {
                boneCell.UpdateVectorSlider();
            }
        }
    }

    struct CachedBonesTransform
    {
        public CachedBonesTransform(Vector3 position, Vector3 angles, Vector3 scale)
        {
            this.position = position;
            this.angles = angles;
            this.scale = scale;
        }

        public readonly Vector3 position;
        public readonly Vector3 angles;
        public readonly Vector3 scale;
    }
}
