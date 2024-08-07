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
    public class AnimatorPlayer {
        public IAnimator animator {get;}
        public List<IAnimationClip> animations = new List<IAnimationClip>();
        public bool shouldIgnoreMasterToggle = false;

        BonesManager bonesManager;
        private List<Transform> bones = new List<Transform>();
        private List<SkinnedMeshRenderer> skinnedMeshes = new();
        private List<MeshRenderer> extraMeshes = new();

        public IAnimationClip overridingAnimation;
        private IAnimationClip lastCurrentAnimation;
        private IRuntimeAnimatorController originalAnimatorController;
        private IAnimatorOverrideController animatorOverrideController = null;

        public List<IAnimationClip> favAnimations {get;}

        public AnimatorPlayer(Behaviour animator){
            this.animator = new IAnimator(animator);
            if (this.animator.runtimeAnimatorController != null){
                this.animations = this.animator.runtimeAnimatorController.animationClips.OrderBy(x=>x.name).Where(c => c.length > 0).Distinct().ToList();
                // Simple heuristic to try and find the player character
                this.shouldIgnoreMasterToggle = animator.gameObject.name.IndexOf("play", 0, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            this.originalAnimatorController = this.animator.runtimeAnimatorController;

            IAnimatorClipInfo[] playingAnimations = this.animator.GetCurrentAnimatorClipInfo(0);
            lastCurrentAnimation = playingAnimations.Count() != 0 ? playingAnimations[0].clip : null;
            this.overridingAnimation = lastCurrentAnimation != null ? lastCurrentAnimation : (animations.Count > 0 ? animations[0] : null);

            this.favAnimations = new List<IAnimationClip>();

            SearchMeshes();
        }

        public void SearchMeshes(){
            skinnedMeshes = new List<SkinnedMeshRenderer>(animator.wrappedObject.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(false));
            extraMeshes = new List<MeshRenderer>(animator.wrappedObject.gameObject.GetComponentsInChildren<MeshRenderer>(false));
        }

        // Include the animations being played in other layers
        private List<IAnimationClip> GetAllCurrentlyPlayingAnimations(){
            List<IAnimationClip> allAnimations = new List<IAnimationClip>();
            for (int layer = 0; layer < animator.layerCount; layer++){
                allAnimations.AddRange(animator.GetCurrentAnimatorClipInfo(layer).Select(ainfo => ainfo.clip).ToList());
            }
            return allAnimations;
        }

        public void ResetAnimation(){
            if (bonesManager != null) bonesManager.turnOffAnimatorToggle.isOn = true;
            // Let the game change animations again
            animator.StopPlayback();
            if (originalAnimatorController != null && animator.wrappedObject != null){
                if (animator.runtimeAnimatorController != null && originalAnimatorController != null){
                    animator.runtimeAnimatorController = originalAnimatorController;

                    animatorOverrideController = null;
                }
            }
        }

        public void PlayOverridingAnimation(float normalizedTime){
            ResetAnimation();
            if (animatorOverrideController == null){
                animatorOverrideController = new IAnimatorOverrideController();
                animatorOverrideController.runtimeAnimatorController = originalAnimatorController;
                // Actually uses runtimeAnimatorController on the original object, should make the wrapper class a child of the runtimeAnimatorController instead
                animator.runtimeAnimatorControllerOverride = animatorOverrideController;

                // Block the game from changing animations
                animator.StartPlayback();
            }

            // Restore previous animation
            if (lastCurrentAnimation != null){
                animatorOverrideController[lastCurrentAnimation] = lastCurrentAnimation;
            }

            IAnimationClip currentAnimation = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
            animatorOverrideController[currentAnimation] = overridingAnimation;
            animator.Play(currentAnimation.name, normalizedTime);

            lastCurrentAnimation = currentAnimation;
        }

        public void FavAnimation(IAnimationClip animation){
            favAnimations.Add(animation);
            favAnimations.OrderBy(x => x.name);
        }

        public void UnfavAnimation(IAnimationClip animation){
            favAnimations.Remove(animation);
        }

        public bool IsAnimationFaved(IAnimationClip animation){
            return favAnimations.Contains(animation);
        }

        public override string ToString(){
            return animator.name;
        }

        public bool enabled
        {
            get {
                return animator.enabled;
            }
            set {
                animator.enabled = value;
            }
        }

        private List<Transform> GetMeshesTransforms(){
            List<Transform> meshes = new List<Transform>();

            foreach (SkinnedMeshRenderer skinnedMesh in skinnedMeshes) {
                meshes.AddRange(skinnedMesh.bones);
            }
            meshes.AddRange(extraMeshes.Select(m => m.transform));
            meshes.RemoveAll(item => item == null);
            return meshes.GroupBy(b => b.name).Select(b => b.First()).ToList().OrderBy(b => b.name).ToList();
        }

        public void OpenBonesPanel(){
            if (skinnedMeshes.Count == 0 && extraMeshes.Count == 0) return;
            if (bonesManager == null){
                bonesManager = new BonesManager(UIManager.GetPanel<UnityExplorer.UI.Panels.AnimatorPanel>(UIManager.Panels.AnimatorPanel).Owner, GetMeshesTransforms(), animator);
            }
            bonesManager.SetActive(true);
        }

        public void MaybeResetBonesPanel(){
            if (bonesManager == null) return;
            bonesManager.RefreshBones(GetMeshesTransforms());
        }

        public void SetMeshesEnabled(bool value){
            foreach (SkinnedMeshRenderer skinnedMesh in skinnedMeshes) {
                skinnedMesh.TryCast<Renderer>().enabled = value;
            }

            foreach (MeshRenderer meshRenderer in extraMeshes) {
                meshRenderer.gameObject.SetActive(value);
            }
        }

        public bool IsMeshHidden(){
            // Could maybe save a variable to set on SetMeshesEnabled instead
            return skinnedMeshes.Any( m => m.TryCast<Renderer>().enabled) || extraMeshes.Any( m => m.gameObject.activeSelf);
        }
    }

    public class IAnimator
    {
        Behaviour _animator;
        Type realType;
        public Behaviour wrappedObject => _animator;

        public IAnimator(Behaviour animator){
            _animator = animator;
            realType = ReflectionUtility.GetTypeByName("UnityEngine.Animator");
        }

        public IRuntimeAnimatorController runtimeAnimatorController
        {
            get {
                PropertyInfo animatorRuntimeAnimatorController = realType.GetProperty("runtimeAnimatorController");
                object runtimeAnimatorControllerObject = animatorRuntimeAnimatorController.GetValue(_animator.TryCast(), null);
                if (runtimeAnimatorControllerObject == null)
                    return null;
                return new IRuntimeAnimatorController(runtimeAnimatorControllerObject);
            }
            set {
                PropertyInfo animatorRuntimeAnimatorController = realType.GetProperty("runtimeAnimatorController");
                animatorRuntimeAnimatorController.SetValue(_animator.TryCast(), value.wrappedObject.TryCast(), null);
            }
        }

        public IAnimatorOverrideController runtimeAnimatorControllerOverride
        {
            set {
                PropertyInfo animatorRuntimeAnimatorController = realType.GetProperty("runtimeAnimatorController");
                animatorRuntimeAnimatorController.SetValue(_animator.TryCast(), value.wrappedObject.TryCast(), null);
            }
        }

        public IAnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layer){
            MethodInfo getCurrentAnimatorClipInfo = realType.GetMethod("GetCurrentAnimatorClipInfo", new Type[] {typeof(int)});
            object resultArray = getCurrentAnimatorClipInfo.Invoke(_animator.TryCast(), new object[] {layer});

            if (resultArray is Array sourceArray){
                List<IAnimatorClipInfo> convertedList = new List<IAnimatorClipInfo>();

                foreach (var item in sourceArray)
                {
                    var convertedItem = new IAnimatorClipInfo(item.TryCast<object>());
                    convertedList.Add(convertedItem);
                }
                return convertedList.ToArray();
            }
#if CPP
            Type genericTypeDefinition = typeof(Il2CppStructArray<>); 
            Type animatirClipInfoType = ReflectionUtility.GetTypeByName("UnityEngine.AnimatorClipInfo");
            Type constructedType = genericTypeDefinition.MakeGenericType(animatirClipInfoType);

            object castedResult = Convert.ChangeType(resultArray, constructedType);
            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(animatirClipInfoType);
            MethodInfo getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
            if (getEnumeratorMethod != null)
            {
                object enumerator = getEnumeratorMethod.Invoke(castedResult, null);
                MethodInfo moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                PropertyInfo currentProperty = enumerator.GetType().GetProperty("Current");
                List<IAnimatorClipInfo> convertedIL2CPPList = new List<IAnimatorClipInfo>();

                // Iterate over the elements using reflection
                while ((bool)moveNextMethod.Invoke(enumerator, null))
                {
                    object item = currentProperty.GetValue(enumerator);
                    var convertedItem = new IAnimatorClipInfo(item.TryCast<object>());
                    convertedIL2CPPList.Add(convertedItem);
                }
                return convertedIL2CPPList.ToArray(); 
            }
#endif
            return new IAnimatorClipInfo[] {};
        }

        public string name
        {
            get {
                PropertyInfo animatorName = realType.GetProperty("name");
                return animatorName.GetValue(_animator.TryCast(), null).ToString();
            }
        }

        public int layerCount
        {
            get {
                PropertyInfo animatorLayerCount = realType.GetProperty("layerCount");
                return (int) animatorLayerCount.GetValue(_animator.TryCast(), null);
            }
        }

        public void Play(string animatorClip, float normalizedTime){
            MethodInfo play = realType.GetMethod("Play", new Type[] {typeof(string), typeof(int), typeof(float)});
            play.Invoke(_animator.TryCast(), new object[] {animatorClip, -1, normalizedTime});
        }

        public float speed
        {
            get {
                PropertyInfo animatorSpeed = realType.GetProperty("speed");
                return (float) animatorSpeed.GetValue(_animator.TryCast(), null);
            }
            set {
                PropertyInfo animatorSpeed = realType.GetProperty("speed");
                animatorSpeed.SetValue(_animator.TryCast(), value, null);
            }
        }

        public bool enabled
        {
            get {
                PropertyInfo animatorEnabled = realType.GetProperty("enabled");
                return (bool) animatorEnabled.GetValue(_animator.TryCast(), null);
            }
            set {
                PropertyInfo animatorEnabled = realType.GetProperty("enabled");
                animatorEnabled.SetValue(_animator.TryCast(), value, null);
            }
        }

        public GameObject gameObject
        {
            get {
                PropertyInfo animatorGameObject = realType.GetProperty("gameObject");
                return (GameObject) animatorGameObject.GetValue(_animator.TryCast(), null);
            }
        }

        public void StopPlayback(){
            MethodInfo StopPlayBackMethod = realType.GetMethod("StopPlayback", Type.EmptyTypes);
            StopPlayBackMethod.Invoke(_animator.TryCast(), null);
        }

        public void StartPlayback(){
            MethodInfo StartPlaybackMethod = realType.GetMethod("StartPlayback", Type.EmptyTypes);
            StartPlaybackMethod.Invoke(_animator.TryCast(), null);
        }
    }

    public class IAnimationClip : IEquatable<IAnimationClip>
    {
        object _animationClip;
        public Type realType;
        public object wrappedObject => _animationClip;

        public IAnimationClip(object animationClip){
            _animationClip = animationClip;
            realType = ReflectionUtility.GetTypeByName("UnityEngine.AnimationClip");
        }

        public string name
        {
            get {
                PropertyInfo animatorName = realType.GetProperty("name");
                return animatorName.GetValue(_animationClip.TryCast(), null).ToString();
            }
        }

        public float length
        {
            get {
                PropertyInfo animatorName = realType.GetProperty("length");
                return (float) animatorName.GetValue(_animationClip.TryCast(), null);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (!(obj is IAnimationClip))
            {
                return false;
            }

            return Equals(obj as IAnimationClip);
        }

        public bool Equals(IAnimationClip other)
        {
            if (other == null)
            {
                return false;
            }

            return name == other.name;
        }

        public override int GetHashCode()
        {
            return name.GetHashCode();
        }
    }

    public class IAnimatorClipInfo
    {
        object _animationClipInfo;
        Type realType;

        public IAnimatorClipInfo(object animationClipInfo){
            _animationClipInfo = animationClipInfo;
            realType = ReflectionUtility.GetTypeByName("UnityEngine.AnimatorClipInfo");
        }

        public IAnimationClip clip
        {
            get {
                PropertyInfo animationClipInfoClip = realType.GetProperty("clip");
                return new IAnimationClip(animationClipInfoClip.GetValue(_animationClipInfo.TryCast(), null));
            }
        }
    }

    public class IRuntimeAnimatorController
    {
        object _runtimeAnimatorController;
        Type realType;
        public object wrappedObject => _runtimeAnimatorController;

        public IRuntimeAnimatorController(object runtimeAnimatorController){
            _runtimeAnimatorController = runtimeAnimatorController;
            realType = ReflectionUtility.GetTypeByName("UnityEngine.RuntimeAnimatorController");
        }

        public IAnimationClip this[string key]
        {
            get
            {
                PropertyInfo indexerProperty = realType.GetProperty("Item", new[] { typeof(string) });
                return new IAnimationClip(indexerProperty.GetValue(_runtimeAnimatorController.TryCast(), new object[] { key }));
            }
            set
            {
                PropertyInfo indexerProperty = realType.GetProperty("Item", new[] { typeof(string) });
                indexerProperty.SetValue(_runtimeAnimatorController.TryCast(), value, new object[] { key });
            }
        }
        
        public IAnimationClip[] animationClips
        {
            get {
                PropertyInfo runtimeAnimatorControllerAnimationClips = realType.GetProperty("animationClips");
                object resultArray = runtimeAnimatorControllerAnimationClips.GetValue(_runtimeAnimatorController.TryCast(), null);

                if (resultArray is Array sourceArray){
                    List<IAnimationClip> convertedList = new List<IAnimationClip>();

                    foreach (var item in sourceArray)
                    {
                        var convertedItem = new IAnimationClip(item.TryCast<object>());
                        convertedList.Add(convertedItem);
                    }
                    return convertedList.ToArray();
                }
#if CPP
                Type genericTypeDefinition = typeof(Il2CppReferenceArray<>);
                Type animationClipType = ReflectionUtility.GetTypeByName("UnityEngine.AnimationClip");
                Type constructedType = genericTypeDefinition.MakeGenericType(animationClipType);

                object castedResult = Convert.ChangeType(resultArray, constructedType);
                Type enumerableType = typeof(IEnumerable<>).MakeGenericType(animationClipType);
                MethodInfo getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
                if (getEnumeratorMethod != null)
                {
                    object enumerator = getEnumeratorMethod.Invoke(castedResult, null);
                    MethodInfo moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                    PropertyInfo currentProperty = enumerator.GetType().GetProperty("Current");
                    List<IAnimationClip> convertedIL2CPPList = new List<IAnimationClip>();

                    // Iterate over the elements using reflection
                    while ((bool)moveNextMethod.Invoke(enumerator, null))
                    {
                        object item = currentProperty.GetValue(enumerator);
                        var convertedItem = new IAnimationClip(item.TryCast<object>());
                        convertedIL2CPPList.Add(convertedItem);
                    }
                    return convertedIL2CPPList.ToArray(); 
                }
#endif
                return new IAnimationClip[] {};
            }
        }
    }

    public class IAnimatorOverrideController
    {
        object _animatorOverrideController;
        Type realType;
        public object wrappedObject => _animatorOverrideController;

        public IAnimatorOverrideController(){
            realType = ReflectionUtility.GetTypeByName("UnityEngine.AnimatorOverrideController");
            ConstructorInfo constructor = realType.GetConstructor(Type.EmptyTypes);
            _animatorOverrideController = constructor.Invoke(null);
        }

        public IRuntimeAnimatorController runtimeAnimatorController
        {
            get {
                PropertyInfo animatorOverrideControllerRuntimeAnimatorController = realType.GetProperty("runtimeAnimatorController");
                return new IRuntimeAnimatorController(animatorOverrideControllerRuntimeAnimatorController.GetValue(_animatorOverrideController.TryCast(), null));
            }

            set {
                PropertyInfo animatorOverrideControllerRuntimeAnimatorController = realType.GetProperty("runtimeAnimatorController");
                animatorOverrideControllerRuntimeAnimatorController.SetValue(_animatorOverrideController.TryCast(), value.wrappedObject.TryCast(), null);
            }
        }

        public IAnimationClip this[IAnimationClip clip]
        {
            get
            {
                PropertyInfo indexerProperty = realType.GetProperty("Item", new[] { typeof(string) });
                if (indexerProperty != null){
                    return new IAnimationClip(indexerProperty.GetValue(_animatorOverrideController.TryCast(), new object[] { clip.name }));
                }

                indexerProperty = realType.GetProperty("Item", new[] { clip.realType });
                if (indexerProperty != null){
                    return new IAnimationClip(indexerProperty.GetValue(_animatorOverrideController.TryCast(), new object[] { clip.wrappedObject }));
                }

                return null;
            }
            set
            {
                PropertyInfo indexerProperty = realType.GetProperty("Item", new[] { typeof(string) });
                if (indexerProperty != null){
                    indexerProperty.SetValue(_animatorOverrideController.TryCast(), value.wrappedObject.TryCast(), new object[] { clip.name });
                    return;
                }

                indexerProperty = realType.GetProperty("Item", new[] { clip.realType });
                if (indexerProperty != null){
                    indexerProperty.SetValue(_animatorOverrideController.TryCast(), value.wrappedObject.TryCast(), new object[] { clip.wrappedObject });
                    return;
                }
            }
        }
    }
}
