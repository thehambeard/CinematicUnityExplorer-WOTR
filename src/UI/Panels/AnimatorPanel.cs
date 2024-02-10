using UnityExplorer.CacheObject;
using UnityExplorer.ObjectExplorer;
using UnityExplorer.Inspectors;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;
using UnityEngine;
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#endif
#if INTEROP
using Il2CppInterop.Runtime.Injection;
#endif

namespace UnityExplorer.UI.Panels
{
    internal class AnimatorPanel : UEPanel
    {
        public AnimatorPanel(UIBase owner) : base(owner)
        {
        }

        public override string Name => "Animator";
        public override UIManager.Panels PanelType => UIManager.Panels.AnimatorPanel;
        public override int MinWidth => 500;
        public override int MinHeight => 500;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;
        List<GameObject> UIElements = new List<GameObject>();

        internal Dictionary<string,List<Behaviour>> animators = new Dictionary<string,List<Behaviour>>();
        internal Dictionary<string,List<Toggle>> animatorToggles = new Dictionary<string,List<Toggle>>();
        internal Dictionary<string,bool> ignoresMasterToggler = new Dictionary<string,bool>();
        Toggle masterAnimatorToggle = new Toggle();

        private void FindAllAnimators(){
            // Enable all animators on refresh
            if (animators.Count != 0) {
                masterAnimatorToggle.isOn = true; // Will also trigger "MasterToggleAnimators(true)"
                animators.Clear();
                animatorToggles.Clear();
                ignoresMasterToggler.Clear();
            }

            Type searchType = ReflectionUtility.GetTypeByName("UnityEngine.Animator");
            searchType = searchType is Type type ? type : searchType.GetActualType();
            List<Behaviour> animatorsList = RuntimeHelper.FindObjectsOfTypeAll(searchType).Select(obj => obj.TryCast<Behaviour>())
            .Where(a => a.GetComponentsInChildren<SkinnedMeshRenderer>(false).Length != 0 && a.enabled && a.GetComponentsInChildren<Rigidbody>(false).Length != 0)
            .ToList();

            foreach(Behaviour animator in animatorsList){
                GameObject gObj = animator.gameObject;
                string key = getNameWithoutClone(gObj.name);

                if (!animators.ContainsKey(key)){
                    animators.Add(key, new List<Behaviour>());
                }

                animators[key].Add(animator);
            }

            BuildAnimatorsTogglers();
        }

        private string getNameWithoutClone(string name){
            string newName = name;
            while (newName.EndsWith("(Clone)")){
                newName = newName.Substring(0, newName.Length - 7);
            }

            return newName;
        }

        private void BuildAnimatorsTogglers(){
            foreach (var comp in UIElements){
                UnityEngine.Object.Destroy(comp);
                //UIElements.Remove(comp);
            }

            foreach (KeyValuePair<string, List<Behaviour>> entry in animators)
            {
                // A weird canse insensitive "Contains" to identify the player
                ignoresMasterToggler[entry.Key] = entry.Key.IndexOf("play", 0, StringComparison.OrdinalIgnoreCase) >= 0;

                GameObject horiGroup = UIFactory.CreateHorizontalGroup(ContentRoot, $"{entry.Key}_Group", false, false, true, false, 4,
                    default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
                UIFactory.SetLayoutElement(horiGroup, minHeight: 25, flexibleWidth: 9999);
                UIElements.Add(horiGroup);

                // is Affected by Master Toggler toggle
                Toggle ignoresMasterTogglerToggle;
                GameObject ignoresMasterTogglerObj = UIFactory.CreateToggle(horiGroup, $"Toggle {entry.Key}", out ignoresMasterTogglerToggle, out Text ignoresMasterTogglerText);
                UIFactory.SetLayoutElement(ignoresMasterTogglerObj, minHeight: 25);
                ignoresMasterTogglerToggle.isOn = ignoresMasterToggler[entry.Key];
                ignoresMasterTogglerToggle.onValueChanged.AddListener(value => ignoresMasterToggler[entry.Key] = value);
                ignoresMasterTogglerText.text = entry.Key;
                
                foreach (Behaviour entityAnimator in entry.Value){
                    Toggle animatorToggle;
                    GameObject animatorObj = UIFactory.CreateToggle(horiGroup, $"Toggle {entry.Key}", out animatorToggle, out Text animatorToggleText);

                    if (!animatorToggles.ContainsKey(entry.Key)){
                        animatorToggles.Add(entry.Key, new List<Toggle>());
                    }
                    animatorToggles[entry.Key].Add(animatorToggle);

                    UIFactory.SetLayoutElement(animatorObj, minHeight: 25);
                    animatorToggle.isOn = true;
                    animatorToggle.onValueChanged.AddListener(value => entityAnimator.enabled = value);
                }

            }
        }

        public void MasterToggleAnimators(bool enable){
            // Load animators for the first time if there are not any
            if (animators.Count == 0) FindAllAnimators();
            foreach (KeyValuePair<string, List<Behaviour>> entry in animators)
            {
                if (!ignoresMasterToggler[entry.Key]){
                    foreach (Toggle animatorToggle in animatorToggles[entry.Key]){
                        animatorToggle.isOn = enable;
                    }
                }
            }
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

            GameObject animatorObj = UIFactory.CreateToggle(firstGroup, $"Master Animation Toggle", out masterAnimatorToggle, out Text masterAnimatorText);
            UIFactory.SetLayoutElement(animatorObj, minHeight: 25);
            masterAnimatorToggle.isOn = true;
            masterAnimatorToggle.onValueChanged.AddListener(value => MasterToggleAnimators(value));
            masterAnimatorText.text = "Master Toggler";

            GameObject secondGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "MainOptions", false, false, true, true, 3,
            default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(firstGroup, minHeight: 25, flexibleWidth: 9999);

            Text isPlayerLabel = UIFactory.CreateLabel(secondGroup, "IsAffectedByMasterToggler", "Ignores Master Toggler", TextAnchor.MiddleLeft, Color.white, false, 15);
            UIFactory.SetLayoutElement(isPlayerLabel.gameObject, minHeight: 25, minWidth: 25, flexibleWidth: 0);

            Text enableLabel = UIFactory.CreateLabel(secondGroup, "EnableLabel", "Enable   ", TextAnchor.MiddleRight, Color.white, false, 15);
            UIFactory.SetLayoutElement(enableLabel.gameObject, minHeight: 25, minWidth: 75, flexibleWidth: 9999);
        }
    }
}
