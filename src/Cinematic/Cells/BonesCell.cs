using UnityExplorer.UI.Widgets;
using UniverseLib.Input;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.UI.Panels
{
    public class BonesCell : ICell
    {
        public Transform bone;
        public TransformControls transformControls;

        Text boneName;
        ComponentControl positionControl;
        ComponentControl rotationControl;
        ComponentControl scaleControl;
        public AxisComponentControl CurrentSlidingAxisControl { get; set; }
        public BonesManager Owner;

        // ICell
        public float DefaultHeight => 25f;
        public GameObject UIRoot { get; set; }
        public RectTransform Rect { get; set; }

        public bool Enabled => UIRoot.activeSelf;
        public void Enable() => UIRoot.SetActive(true);
        public void Disable() => UIRoot.SetActive(false);

        public void SetBone(Transform bone, BonesManager bonesManager){
            this.bone = bone;
            boneName.text = bone.name;
            Owner = bonesManager;
        }

        public virtual GameObject CreateContent(GameObject parent)
        {
            UIRoot = UIFactory.CreateUIObject("BoneCell", parent, new Vector2(25, 25));
            Rect = UIRoot.GetComponent<RectTransform>();
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(UIRoot, false, false, true, true, 3);
            UIFactory.SetLayoutElement(UIRoot, minHeight: 25, minWidth: 50, flexibleWidth: 9999);

            GameObject header = UIFactory.CreateUIObject("BoneHeader", UIRoot);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(header, false, false, true, true, 4, childAlignment: TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(header, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 800);

            boneName = UIFactory.CreateLabel(header, $"BoneLabel", "");
            UIFactory.SetLayoutElement(boneName.gameObject, minWidth: 100, minHeight: 25);

            GameObject headerButtons = UIFactory.CreateUIObject("BoneHeader", header);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(headerButtons, false, false, true, true, 4, childAlignment: TextAnchor.MiddleRight);
            UIFactory.SetLayoutElement(headerButtons, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 800);

            ButtonRef restoreBoneStateButton = UIFactory.CreateButton(headerButtons, "RestoreBoneState", "Restore State");
            UIFactory.SetLayoutElement(restoreBoneStateButton.GameObject, minWidth: 125, minHeight: 25);
            restoreBoneStateButton.OnClick += RestoreBoneState;

            positionControl = ComponentControl.Create(this, UIRoot, "Local Position", TransformType.LocalPosition, 0.01f);
            rotationControl = ComponentControl.Create(this, UIRoot, "Rotation", TransformType.Rotation, 10f);
            scaleControl = ComponentControl.Create(this, UIRoot, "Scale", TransformType.Scale, 0.1f);

            return UIRoot;
        }

        private void RestoreBoneState(){
            Owner.RestoreBoneState(bone.name);
        }

        // TransformControls-like functions
        public void UpdateTransformControlValues(bool force){
            positionControl.Update(force);
            rotationControl.Update(force);
            scaleControl.Update(force);
        }

        public void UpdateVectorSlider()
        {
            AxisComponentControl control = CurrentSlidingAxisControl;

            if (control == null)
                return;

            if (!IInputManager.GetMouseButton(0))
            {
                control.slider.value = 0f;
                control = null;
                return;
            }

            AxisControlOperation(control.slider.value, control.parent, control.axis);

            if (Owner.turnOffAnimatorToggle.isOn) Owner.turnOffAnimatorToggle.isOn = false;
        }

        public void AxisControlOperation(float value, ComponentControl parent, int axis)
        {
            Transform transform = bone;

            Vector3 vector = parent.Type switch
            {
                TransformType.Position => transform.position,
                TransformType.LocalPosition => transform.localPosition,
                TransformType.Rotation => transform.localEulerAngles,
                TransformType.Scale => transform.localScale,
                _ => throw new NotImplementedException()
            };

            // apply vector value change
            switch (axis)
            {
                case 0:
                    vector.x += value; break;
                case 1:
                    vector.y += value; break;
                case 2:
                    vector.z += value; break;
            }

            // set vector back to transform
            switch (parent.Type)
            {
                case TransformType.Position:
                    transform.position = vector; break;
                case TransformType.LocalPosition:
                    transform.localPosition = vector; break;
                case TransformType.Rotation:
                    transform.localEulerAngles = vector; break;
                case TransformType.Scale:
                    transform.localScale = vector; break;
            }

            UpdateTransformControlValues(false);
        }
    }

    // Duplication of Vector3Control class
    public class ComponentControl
    {
        public BonesCell Owner { get; }
        public Transform Transform => Owner.bone;
        public TransformType Type { get; }

        public InputFieldRef MainInput { get; }

        public AxisComponentControl[] axisComponentControl { get; } = new AxisComponentControl[3];

        public InputFieldRef IncrementInput { get; set; }
        public float Increment { get; set; } = 0.1f;

        Vector3 lastValue;

        Vector3 CurrentValue => Type switch
        {
            TransformType.Position => Transform.position,
            TransformType.LocalPosition => Transform.localPosition,
            TransformType.Rotation => Transform.localEulerAngles,
            TransformType.Scale => Transform.localScale,
            _ => throw new NotImplementedException()
        };

        public ComponentControl(BonesCell cell, TransformType type, InputFieldRef input)
        {
            this.Owner = cell;
            this.Type = type;
            this.MainInput = input;
        }

        public void Update(bool force)
        {
            // Probably not needed
            if (Transform == null) return;

            Vector3 currValue = CurrentValue;
            if (force || (!MainInput.Component.isFocused && !lastValue.Equals(currValue)))
            {
                MainInput.Text = ParseUtility.ToStringForInput<Vector3>(currValue);
                lastValue = currValue;
            }
        }

        void OnTransformInputEndEdit(TransformType type, string input)
        {
            switch (type)
            {
                case TransformType.Position:
                    {
                        if (ParseUtility.TryParse(input, out Vector3 val, out _))
                            Transform.position = val;
                    }
                    break;
                case TransformType.LocalPosition:
                    {
                        if (ParseUtility.TryParse(input, out Vector3 val, out _))
                            Transform.localPosition = val;
                    }
                    break;
                case TransformType.Rotation:
                    {
                        if (ParseUtility.TryParse(input, out Vector3 val, out _))
                            Transform.localEulerAngles = val;
                    }
                    break;
                case TransformType.Scale:
                    {
                        if (ParseUtility.TryParse(input, out Vector3 val, out _))
                            Transform.localScale = val;
                    }
                    break;
            }

            Owner.UpdateTransformControlValues(true);
            if (Owner.Owner.turnOffAnimatorToggle.isOn) Owner.Owner.turnOffAnimatorToggle.isOn = false;
        }

        void IncrementInput_OnEndEdit(string value)
        {
            if (!ParseUtility.TryParse(value, out float increment, out _))
                IncrementInput.Text = ParseUtility.ToStringForInput<float>(Increment);
            else
            {
                Increment = increment;
                foreach (AxisComponentControl slider in axisComponentControl)
                {
                    slider.slider.minValue = -increment;
                    slider.slider.maxValue = increment;
                }
            }
        }

        public static ComponentControl Create(BonesCell cell, GameObject transformGroup, string title, TransformType type, float increment)
        {
            GameObject rowObj = UIFactory.CreateUIObject($"Row_{title}", transformGroup);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(rowObj, false, false, true, true, 5, 0, 0, 0, 0, default);
            UIFactory.SetLayoutElement(rowObj, minHeight: 25, flexibleWidth: 9999);

            Text titleLabel = UIFactory.CreateLabel(rowObj, "PositionLabel", title, TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(titleLabel.gameObject, minHeight: 25, minWidth: 110);

            InputFieldRef inputField = UIFactory.CreateInputField(rowObj, "InputField", "...");
            UIFactory.SetLayoutElement(inputField.Component.gameObject, minHeight: 25, minWidth: 100, flexibleWidth: 999);

            ComponentControl control = new(cell, type, inputField);

            inputField.Component.GetOnEndEdit().AddListener((string value) => { control.OnTransformInputEndEdit(type, value); });
            control.Increment = increment;

            control.axisComponentControl[0] = AxisComponentControl.Create(rowObj, "X", 0, control);
            control.axisComponentControl[1] = AxisComponentControl.Create(rowObj, "Y", 1, control);
            control.axisComponentControl[2] = AxisComponentControl.Create(rowObj, "Z", 2, control);

            control.IncrementInput = UIFactory.CreateInputField(rowObj, "IncrementInput", "...");
            control.IncrementInput.Text = increment.ToString();
            UIFactory.SetLayoutElement(control.IncrementInput.GameObject, minWidth: 30, flexibleWidth: 0, minHeight: 25);
            control.IncrementInput.Component.GetOnEndEdit().AddListener(control.IncrementInput_OnEndEdit);

            if (type == TransformType.Scale){
                GameObject extraRowObj = UIFactory.CreateUIObject($"Row_UniformScale", transformGroup);
                UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(extraRowObj, false, false, true, true, 5, 0, 0, 0, 0, default);
                UIFactory.SetLayoutElement(extraRowObj, minHeight: 25, flexibleWidth: 9999);

                Text uniformScaleTitleLabel = UIFactory.CreateLabel(extraRowObj, "UniformScaleLabel", "Uniform Scale:", TextAnchor.MiddleRight, Color.grey);
                UIFactory.SetLayoutElement(uniformScaleTitleLabel.gameObject, minHeight: 25, minWidth: 110);

                GameObject uniformScaleControlObj = UIFactory.CreateSlider(extraRowObj, "UniformScaleSlider", out Slider uniformScaleControl);
                UIFactory.SetLayoutElement(uniformScaleControlObj, minHeight: 25, minWidth: 200, flexibleHeight: 0);
                uniformScaleControl.minValue = 0.0001f;
                uniformScaleControl.maxValue = 10;
                uniformScaleControl.value = 1;
                uniformScaleControl.onValueChanged.AddListener((float val) => { cell.bone.localScale = new Vector3(val, val, val); });
            }

            return control;
        }
    }
    
    // // Duplication of AxisControl class
    public class AxisComponentControl
    {
        public readonly ComponentControl parent;

        public readonly int axis;
        public readonly Slider slider;

        public AxisComponentControl(int axis, Slider slider, ComponentControl parentControl)
        {
            this.parent = parentControl;
            this.axis = axis;
            this.slider = slider;
        }

        void OnVectorSliderChanged(float value)
        {
            parent.Owner.CurrentSlidingAxisControl = value == 0f ? null : this;
        }

        void OnVectorMinusClicked()
        {
            parent.Owner.AxisControlOperation(-this.parent.Increment, this.parent, this.axis);
        }

        void OnVectorPlusClicked()
        {
            parent.Owner.AxisControlOperation(this.parent.Increment, this.parent, this.axis);
        }

        public static AxisComponentControl Create(GameObject parent, string title, int axis, ComponentControl owner)
        {
            Text label = UIFactory.CreateLabel(parent, $"Label_{title}", $"{title}:", TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(label.gameObject, minHeight: 25, minWidth: 30);

            GameObject sliderObj = UIFactory.CreateSlider(parent, $"Slider_{title}", out Slider slider);
            UIFactory.SetLayoutElement(sliderObj, minHeight: 25, minWidth: 75, flexibleWidth: 0);
            slider.m_FillImage.color = Color.clear;

            slider.minValue = -owner.Increment;
            slider.maxValue = owner.Increment;

            AxisComponentControl sliderControl = new(axis, slider, owner);

            slider.onValueChanged.AddListener(sliderControl.OnVectorSliderChanged);

            ButtonRef minusButton = UIFactory.CreateButton(parent, "MinusIncrementButton", "-");
            UIFactory.SetLayoutElement(minusButton.GameObject, minWidth: 20, flexibleWidth: 0, minHeight: 25);
            minusButton.OnClick += sliderControl.OnVectorMinusClicked;

            ButtonRef plusButton = UIFactory.CreateButton(parent, "PlusIncrementButton", "+");
            UIFactory.SetLayoutElement(plusButton.GameObject, minWidth: 20, flexibleWidth: 0, minHeight: 25);
            plusButton.OnClick += sliderControl.OnVectorPlusClicked;

            return sliderControl;
        }
    }
}
