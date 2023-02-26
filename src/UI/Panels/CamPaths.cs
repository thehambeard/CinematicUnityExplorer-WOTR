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

        static ButtonRef startButton;
		public List<Transform> controlPoints = new List<Transform>();

        // ~~~~~~~~ UI construction / callbacks ~~~~~~~~

        protected override void ConstructPanelContent()
        {   
            startButton = UIFactory.CreateButton(ContentRoot, "Start", "Start CamPath");
            UIFactory.SetLayoutElement(startButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            startButton.OnClick += StartButton_OnClick;

            startButton = UIFactory.CreateButton(ContentRoot, "AddCamNode", "Add Cam node");
            UIFactory.SetLayoutElement(startButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            startButton.OnClick += AddNode_OnClick;

        }

        void AddSpacer(int height)
        {
            GameObject obj = UIFactory.CreateUIObject("Spacer", ContentRoot);
            UIFactory.SetLayoutElement(obj, minHeight: height, flexibleHeight: 0);
        }

        void StartButton_OnClick()
        {
            int resolution = 500;
            bool closedLoop = false;
            //float normalExtrusion = 0;
            //float tangentExtrusion = 0;

            if(ExplorerCore.CameraPathsManager == null)
                ExplorerCore.CameraPathsManager = new CatmullRom(controlPoints.ToArray(), resolution, closedLoop);
            else{
                ExplorerCore.CameraPathsManager.Update(controlPoints.ToArray());
                ExplorerCore.CameraPathsManager.Update(resolution, closedLoop);
            }
            
            ExplorerCore.Log("Iniciamos path.");
            ExplorerCore.CameraPathsManager.StartPath();

            ExplorerCore.Log("Clicked start path button.");
        }

        private Transform NewTransform(Transform original){
            GameObject ob = new GameObject("CamPathNode");
            ob.transform.position = original.position;
            //ob.transform.rotation = original.rotation;
            return ob.transform;
        }

        void AddNode_OnClick(){
            Camera freeCam = FreeCamPanel.ourCamera;
            controlPoints.Add(NewTransform(freeCam.transform));
        }
    }
}
