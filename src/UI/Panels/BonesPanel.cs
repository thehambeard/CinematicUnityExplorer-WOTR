using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Panels;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.UI.Panels
{
    public class BonesManager : PanelBase, ICellPoolDataSource<BonesCell>
    {
        public override string Name => $"Bones Manager";
        public override int MinWidth => 1000;
        public override int MinHeight => 800;
        public override Vector2 DefaultAnchorMin => Vector2.zero;
        public override Vector2 DefaultAnchorMax => Vector2.zero;
        public Toggle turnOffAnimatorToggle;
        
        private IAnimator animator;
        private Text skeletonName;
        private List<Transform> bones = new List<Transform>();
        private Dictionary<string, CachedBonesTransform> bonesOriginalState = new();

        public List<BoneTree> boneTrees = new();
        public ScrollPool<BonesCell> boneScrollPool;
        public int ItemCount => boneTrees.Count;
        private bool DoneScrollPoolInit;

        public BonesManager(UIBase owner, List<Transform> bones, IAnimator animator) : base(owner)
        {
            this.bones = bones;
            this.animator = animator;
            skeletonName.text = $"Skeleton: {animator?.name}";
            BuildBoneTrees();
        }

        private void BuildBoneTrees(){
            BoneTree root = new BoneTree(animator.wrappedObject.gameObject, bones);
            if (root.obj != null){
                root.AssignLevels();
                boneTrees.Add(root);
            } else {
                foreach(BoneTree childTree in root.childTrees){
                    childTree.AssignLevels();
                    boneTrees.Add(childTree);
                }
            }
        }

        private void CollapseBoneTrees(){
            boneTrees.Clear();
            BuildBoneTrees();
            boneScrollPool.Refresh(true, true);
        }

        private void ExpandBoneTrees(){
            // We collapse before expanding to start from scratch and dont duplicate nodes
            CollapseBoneTrees();

            List<BoneTree> newBoneTrees = new();

            foreach(BoneTree childTree in boneTrees){
                newBoneTrees.AddRange(childTree.flatten());
            }

            boneTrees = newBoneTrees;
            boneScrollPool.Refresh(true, false);
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

            GameObject header = UIFactory.CreateUIObject("Header", ContentRoot, new Vector2(25, 25));
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(header, false, false, true, true, 4, childAlignment: TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(header, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 800);

            GameObject turnOffAnimatorToggleObj = UIFactory.CreateToggle(header, "Animator toggle", out turnOffAnimatorToggle, out Text turnOffAnimatorToggleText);
            UIFactory.SetLayoutElement(turnOffAnimatorToggleObj, minHeight: 25, flexibleWidth: 9999);
            turnOffAnimatorToggle.onValueChanged.AddListener(OnTurnOffAnimatorToggle);
            turnOffAnimatorToggleText.text = "Toggle animator (needs to be off to move bones)";

            ButtonRef collapseAllButton = UIFactory.CreateButton(header, "CollapseAllButton", "Collapse all");
            UIFactory.SetLayoutElement(collapseAllButton.GameObject, minWidth: 150, minHeight: 25);
            collapseAllButton.OnClick += CollapseBoneTrees;

            ButtonRef expandAllButton = UIFactory.CreateButton(header, "ExpandAllButton", "Expand all");
            UIFactory.SetLayoutElement(expandAllButton.GameObject, minWidth: 150, minHeight: 25);
            expandAllButton.OnClick += ExpandBoneTrees;

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
            if (index >= boneTrees.Count)
            {
                cell.Disable();
                return;
            }

            BoneTree boneTree = boneTrees[index];
            cell.SetBoneTree(boneTree, this);
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

    public class BoneTree
    {
        public GameObject obj;
        public int level;
        public List<BoneTree> childTrees = new();

        public BoneTree(GameObject obj, List<Transform> bones){
            // For some reason comparing GameObjects isn't working as intended in IL2CPP games, therefore we use their instance hash.
#if CPP
            if (bones.Any(bone => bone.gameObject.GetInstanceID() == obj.GetInstanceID())) {
                this.obj = obj;
            }
#else
            if (bones.Contains(obj.transform)) {
                this.obj = obj;
            }
#endif
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                Transform child = obj.transform.GetChild(i);
                if (child.gameObject.activeSelf){
                    childTrees.Add(new BoneTree(child.gameObject, bones));
                }
            }

            Trim();
            childTrees = childTrees.OrderBy(b => b.obj.name).ToList();
        }

        private void Trim(){
            List<BoneTree> newList = new();
            foreach (BoneTree childTree in childTrees)
            {
                if (childTree.obj == null){
                    newList.AddRange(childTree.childTrees);
                } else {
                    newList.Add(childTree);
                }
            }

            this.childTrees = newList;
        }

        // TODO: refactor BoneTree so we don't need to call this after creating an instance.
        public void AssignLevels(){
            AssignLevel(0);
        }

        private void AssignLevel(int distanceFromRoot){
            level = distanceFromRoot;
            foreach (BoneTree childTree in childTrees)
            {
                childTree.AssignLevel(distanceFromRoot + 1);
            }
        }

        public override string ToString(){
            string return_string = "";
            if (obj != null){
                return_string = $"{obj.name} lvl: {level} - ";
            }

            foreach (BoneTree childTree in childTrees)
            {
                return_string = return_string + childTree.ToString();
            }

            return return_string;
        }

        public List<GameObject> getGameObjects(){
            List<GameObject> return_list = new();
            if (obj != null){
                return_list.Add(obj);
            }

            foreach (BoneTree childTree in childTrees)
            {
                return_list.AddRange(childTree.getGameObjects());
            }

            return return_list;
        }

        public List<BoneTree> flatten(){
            List<BoneTree> return_list = new();
            if (obj != null){
                return_list.Add(this);
            }

            foreach (BoneTree childTree in childTrees)
            {
                return_list.AddRange(childTree.flatten());
            }

            return return_list;
        }
    }
}
