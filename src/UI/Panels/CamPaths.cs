using UniverseLib.Input;
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

        public override string Name => "CamPaths";
        public override UIManager.Panels PanelType => UIManager.Panels.CamPaths;
        public override int MinWidth => 400;
        public override int MinHeight => 600;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;
		public List<CatmullRom.PathControlPoint> controlPoints = new List<CatmullRom.PathControlPoint>();
        bool closedLoop;

        // ~~~~~~~~ UI construction / callbacks ~~~~~~~~

        protected override void ConstructPanelContent()
        {   
            ButtonRef startButton = UIFactory.CreateButton(ContentRoot, "Start", "Start CamPath");
            UIFactory.SetLayoutElement(startButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            startButton.OnClick += StartButton_OnClick;

            ButtonRef AddNode = UIFactory.CreateButton(ContentRoot, "AddCamNode", "Add Cam node");
            UIFactory.SetLayoutElement(AddNode.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            AddNode.OnClick += AddNode_OnClick;

            ButtonRef DeletePath = UIFactory.CreateButton(ContentRoot, "DeletePath", "Delete Path");
            UIFactory.SetLayoutElement(DeletePath.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            DeletePath.OnClick += () => {controlPoints.Clear();};

            Toggle closedLoopToggle = new Toggle();
            GameObject toggleObj = UIFactory.CreateToggle(ContentRoot, "Close path in a loop", out closedLoopToggle, out Text toggleText);
            UIFactory.SetLayoutElement(toggleObj, minHeight: 25, flexibleWidth: 9999);
            closedLoopToggle.onValueChanged.AddListener((isClosedLoop) => {closedLoop = isClosedLoop;});
            closedLoopToggle.isOn = false;
            toggleText.text = "Close path in a loop";

        }

        void AddSpacer(int height)
        {
            GameObject obj = UIFactory.CreateUIObject("Spacer", ContentRoot);
            UIFactory.SetLayoutElement(obj, minHeight: height, flexibleHeight: 0);
        }

        void StartButton_OnClick()
        {
            int resolution = 500;
            //float normalExtrusion = 0;
            //float tangentExtrusion = 0;

            if(ExplorerCore.CameraPathsManager == null)
                ExplorerCore.CameraPathsManager = new CatmullRom(controlPoints.ToArray(), resolution, closedLoop);
            else{
                ExplorerCore.CameraPathsManager.Update(controlPoints.ToArray());
                ExplorerCore.CameraPathsManager.Update(resolution, closedLoop);
            }
            
            ExplorerCore.CameraPathsManager.StartPath();
        }

        void AddNode_OnClick(){
            Camera freeCam = FreeCamPanel.ourCamera;
            CatmullRom.PathControlPoint point = new CatmullRom.PathControlPoint(freeCam.transform.position, freeCam.transform.rotation, freeCam.fieldOfView);
            controlPoints.Add(point);
        }
    }
}
