﻿using UniverseLib.Input;
using UniverseLib.UI;
using UniverseLib.UI.Models;
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#endif
#if INTEROP
using Il2CppInterop.Runtime.Injection;
#endif

using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;

namespace UnityExplorer.UI.Panels
{
    public class CamPaths : UEPanel
    {
        public CamPaths(UIBase owner) : base(owner)
        {
        }

        public override string Name => "Cam Paths";
        public override UIManager.Panels PanelType => UIManager.Panels.CamPaths;
        public override int MinWidth => 600;
        public override int MinHeight => 600;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;
		public List<CatmullRom.CatmullRomPoint> controlPoints = new List<CatmullRom.CatmullRomPoint>();
        List<GameObject> UINodes = new List<GameObject>();
        bool closedLoop;
        float time = 10;

        GameObject followObject = null;

        // ~~~~~~~~ UI construction / callbacks ~~~~~~~~

        protected override void ConstructPanelContent()
        {   
            GameObject horiGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "MainOptions", false, false, true, true, 3,
                default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(horiGroup, minHeight: 25, flexibleWidth: 9999);

            ButtonRef startButton = UIFactory.CreateButton(horiGroup, "Start", "▶");
            UIFactory.SetLayoutElement(startButton.GameObject, minWidth: 50, minHeight: 25);
            startButton.ButtonText.fontSize = 20;
            startButton.OnClick += StartButton_OnClick;

            ButtonRef pauseContinueButton = UIFactory.CreateButton(horiGroup, "Pause/Continue", "❚❚/▶");
            UIFactory.SetLayoutElement(pauseContinueButton.GameObject, minWidth: 50, minHeight: 25);
            pauseContinueButton.ButtonText.fontSize = 20;
            pauseContinueButton.OnClick += TogglePause_OnClick;

            ButtonRef stopButton = UIFactory.CreateButton(horiGroup, "Stop", "■");
            UIFactory.SetLayoutElement(stopButton.GameObject, minWidth: 50, minHeight: 25);
            stopButton.ButtonText.fontSize = 20;
            stopButton.OnClick += Stop_OnClick;

            ButtonRef AddNode = UIFactory.CreateButton(horiGroup, "AddCamNode", "+");
            UIFactory.SetLayoutElement(AddNode.GameObject, minWidth: 50, minHeight: 25);
            AddNode.OnClick += AddNode_OnClick;

            ButtonRef DeletePath = UIFactory.CreateButton(horiGroup, "DeletePath", "Delete Path");
            UIFactory.SetLayoutElement(DeletePath.GameObject, minWidth: 150, minHeight: 25);
            DeletePath.OnClick += () => {controlPoints.Clear(); UpdateListNodes();};

            Toggle closedLoopToggle = new Toggle();
            GameObject toggleClosedLoopObj = UIFactory.CreateToggle(horiGroup, "Close path in a loop", out closedLoopToggle, out Text toggleClosedLoopText);
            UIFactory.SetLayoutElement(toggleClosedLoopObj, minHeight: 25, flexibleWidth: 9999);
            closedLoopToggle.onValueChanged.AddListener((isClosedLoop) => {closedLoop = isClosedLoop;});
            closedLoopToggle.isOn = false;
            toggleClosedLoopText.text = "Close path in a loop";

            InputFieldRef TimeInput = null;
            GameObject secondRow = AddInputField("Time", "Path time (in seconds at 60fps):", $"Default: {time}", out TimeInput, Time_OnEndEdit, false);
            TimeInput.Text = time.ToString();
        }

        private void UpdateListNodes(){
            //Refresh list
            foreach (var comp in UINodes){
                UnityEngine.Object.Destroy(comp);
                //UIElements.Remove(comp);
            }

            for(int i = 0; i < controlPoints.Count; i++) {
                CatmullRom.CatmullRomPoint point = controlPoints[i];
                DrawNodeOptions(point, i);
            }
        }

        private void DrawNodeOptions(CatmullRom.CatmullRomPoint point, int index){
            GameObject horiGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "LightOptions", true, false, true, false, 4,
                default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(horiGroup, minHeight: 25, flexibleWidth: 9999);
            UINodes.Add(horiGroup);

            //Move to Camera
            ButtonRef moveToCameraButton = UIFactory.CreateButton(horiGroup, "Copy Camera pos and rot", "Copy Camera pos and rot");
            UIFactory.SetLayoutElement(moveToCameraButton.GameObject, minWidth: 100, minHeight: 25, flexibleWidth: 9999);
            moveToCameraButton.OnClick += () => {
                point.position = followObject != null ? FreeCamPanel.ourCamera.transform.localPosition : FreeCamPanel.ourCamera.transform.position;
                point.rotation = followObject != null ? FreeCamPanel.ourCamera.transform.localRotation : FreeCamPanel.ourCamera.transform.rotation;
                controlPoints[index] = point;

                EventSystemHelper.SetSelectedGameObject(null);
            };

            ButtonRef copyFovButton = UIFactory.CreateButton(horiGroup, "Copy Camera FoV", "Copy Camera FoV");
            UIFactory.SetLayoutElement(copyFovButton.GameObject, minWidth: 100, minHeight: 25, flexibleWidth: 9999);
            copyFovButton.OnClick += () => {point.fov = FreeCamPanel.ourCamera.fieldOfView; controlPoints[index] = point; EventSystemHelper.SetSelectedGameObject(null);};

            ButtonRef moveToPointButton = UIFactory.CreateButton(horiGroup, "Move Cam to Node", "Move Cam to Node");
            UIFactory.SetLayoutElement(moveToPointButton.GameObject, minWidth: 100, minHeight: 25, flexibleWidth: 9999);
            moveToPointButton.OnClick += () => {
                if (followObject != null){
                    FreeCamPanel.ourCamera.transform.localPosition = point.position;
                    FreeCamPanel.ourCamera.transform.localRotation = point.rotation;
                } else {
                    FreeCamPanel.ourCamera.transform.position = point.position;
                    FreeCamPanel.ourCamera.transform.rotation = point.rotation;
                }
                FreeCamPanel.ourCamera.fieldOfView = point.fov;

                EventSystemHelper.SetSelectedGameObject(null);
            };

            //Add node next

            ButtonRef destroyButton = UIFactory.CreateButton(horiGroup, "Delete", "Delete");
            UIFactory.SetLayoutElement(destroyButton.GameObject, minWidth: 80, minHeight: 25, flexibleWidth: 9999);
            destroyButton.OnClick += () => {controlPoints.Remove(point); UpdateListNodes();};

            //ShowPos and rot?
        }

        GameObject AddInputField(string name, string labelText, string placeHolder, out InputFieldRef inputField, Action<string> onInputEndEdit, bool shouldDeleteOnUpdate = true)
        {
            GameObject row = UIFactory.CreateHorizontalGroup(ContentRoot, "Editor Field",
            false, false, true, true, 5, default, new Color(1, 1, 1, 0));
            if(shouldDeleteOnUpdate)
                UINodes.Add(row); //To delete it when we update the node list

            Text posLabel = UIFactory.CreateLabel(row, $"{name}_Label", labelText);
            UIFactory.SetLayoutElement(posLabel.gameObject, minWidth: 80, minHeight: 25);

            inputField = UIFactory.CreateInputField(row, $"{name}_Input", placeHolder);
            UIFactory.SetLayoutElement(inputField.GameObject, minWidth: 50, minHeight: 25);
            inputField.Component.GetOnEndEdit().AddListener(onInputEndEdit);

            return row;
        }

        void StartButton_OnClick()
        {
            if(GetCameraPathsManager()){
                GetCameraPathsManager().setClosedLoop(closedLoop);
                GetCameraPathsManager().setLocalPoints(followObject != null);
                GetCameraPathsManager().setSplinePoints(controlPoints.ToArray());
                GetCameraPathsManager().setTime(time);
                GetCameraPathsManager().StartPath();
                UIManager.ShowMenu = false;
            }

            EventSystemHelper.SetSelectedGameObject(null);
        }

        void TogglePause_OnClick(){
            if(GetCameraPathsManager()){
                GetCameraPathsManager().TogglePause();
            }

            EventSystemHelper.SetSelectedGameObject(null);
        }

        void Stop_OnClick(){
            if (GetCameraPathsManager()){
                GetCameraPathsManager().Stop();
            }

            EventSystemHelper.SetSelectedGameObject(null);
        }

        void AddNode_OnClick(){
            Camera freeCam = FreeCamPanel.ourCamera;
            CatmullRom.CatmullRomPoint point = new CatmullRom.CatmullRomPoint(
                followObject != null ? freeCam.transform.localPosition : freeCam.transform.position,
                followObject != null ? freeCam.transform.localRotation : freeCam.transform.rotation,
                freeCam.fieldOfView
            );

            controlPoints.Add(point);
            UpdateListNodes();

            EventSystemHelper.SetSelectedGameObject(null);
        }

        void Time_OnEndEdit(string input)
        {
            EventSystemHelper.SetSelectedGameObject(null);

            if (!ParseUtility.TryParse(input, out int parsed, out Exception parseEx))
            {
                ExplorerCore.LogWarning($"Could not parse value: {parseEx.ReflectionExToString()}");
                return;
            }

            time = parsed;
            if(GetCameraPathsManager()){
                GetCameraPathsManager().setTime(time);
            }
        }

        private CatmullRom.CatmullRomMover GetCameraPathsManager(){
            return FreeCamPanel.cameraPathMover;
        }

        public void UpdatedFollowObject(GameObject obj){
            if(followObject != null) TranslatePointsToGlobal();
            followObject = obj;
            if (obj != null){
                TranslatePointsToLocal();
            }
            UpdateListNodes();
        }

        void TranslatePointsToGlobal() {
            List<CatmullRom.CatmullRomPoint> newControlPoints = new List<CatmullRom.CatmullRomPoint>();
            foreach(CatmullRom.CatmullRomPoint point in controlPoints){
                Vector3 newPos = followObject.transform.TransformPoint(point.position);
                Quaternion newRot = followObject.transform.rotation * point.rotation;
                CatmullRom.CatmullRomPoint newPoint = new CatmullRom.CatmullRomPoint(newPos, newRot, point.fov);

                newControlPoints.Add(newPoint);
            }

            controlPoints = newControlPoints;
        }

        void TranslatePointsToLocal() {
            List<CatmullRom.CatmullRomPoint> newControlPoints = new List<CatmullRom.CatmullRomPoint>();
            foreach(CatmullRom.CatmullRomPoint point in controlPoints){
                Vector3 newPos = followObject.transform.InverseTransformPoint(point.position);
                Quaternion newRot = Quaternion.Inverse(followObject.transform.rotation) * point.rotation;
                CatmullRom.CatmullRomPoint newPoint = new CatmullRom.CatmullRomPoint(newPos, newRot, point.fov);

                newControlPoints.Add(newPoint);
            }

            controlPoints = newControlPoints;
        }
    }
}
