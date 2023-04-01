using UnityExplorer.UI;
using UniverseLib.Input;
#if CPP
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#else
using Il2CppInterop.Runtime.Injection;
#endif
#endif
using UnityExplorer.UI.Panels;
using System;


namespace UnityExplorer
{
    public class ExplorerBehaviour : MonoBehaviour
    {
        internal static ExplorerBehaviour Instance { get; private set; }

#if CPP
        public ExplorerBehaviour(System.IntPtr ptr) : base(ptr) { }
#endif

        internal static void Setup()
        {
#if CPP
            ClassInjector.RegisterTypeInIl2Cpp<ExplorerBehaviour>();
#endif

            GameObject obj = new("ExplorerBehaviour");
            DontDestroyOnLoad(obj);
            obj.hideFlags = HideFlags.HideAndDontSave;
            Instance = obj.AddComponent<ExplorerBehaviour>();
        }

        internal void Update()
        {
            ExplorerCore.Update();
        }

        // For editor, to clean up objects

        internal void OnDestroy()
        {
            OnApplicationQuit();
        }

        internal bool quitting;

        internal void OnApplicationQuit()
        {
            if (quitting) return;
            quitting = true;

            TryDestroy(UIManager.UIRoot?.transform.root.gameObject);

            TryDestroy((typeof(Universe).Assembly.GetType("UniverseLib.UniversalBehaviour")
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null, null)
                as Component).gameObject);

            TryDestroy(this.gameObject);
        }

        internal void TryDestroy(GameObject obj)
        {
            try
            {
                if (obj)
                    Destroy(obj);
            }
            catch { }
        }
    }

    // Cinematic stuff

    public class TimeScaleController : MonoBehaviour
    {
        internal static TimeScaleController Instance { get; private set; }

#if CPP
        public TimeScaleController(System.IntPtr ptr) : base(ptr) { }
#endif

        public bool pause;
        bool settingTimeScale;

        internal static void Setup()
        {
#if CPP
            ClassInjector.RegisterTypeInIl2Cpp<TimeScaleController>();
#endif

            GameObject obj = new("TimeScaleController");
            DontDestroyOnLoad(obj);
            obj.hideFlags = HideFlags.HideAndDontSave;
            Instance = obj.AddComponent<TimeScaleController>(); 
        }

        public void Update()
        {
            if (InputManager.GetKeyDown(KeyCode.Pause))
            {
                pause = !pause;
                if (pause)
                    SetTimeScale(0f);
                else
                    SetTimeScale(1f);
            }
        }

        void SetTimeScale(float time)
        {
            settingTimeScale = true;
            Time.timeScale = time;
            settingTimeScale = false;
        }
    }

    /*  
        Based of JPBotelho implementation
        https://github.com/JPBotelho/Catmull-Rom-Splines

        Catmull-Rom splines are Hermite curves with special tangent values.
        Hermite curve formula:
        (2t^3 - 3t^2 + 1) * p0 + (t^3 - 2t^2 + t) * m0 + (-2t^3 + 3t^2) * p1 + (t^3 - t^2) * m1
        For points p0 and p1 passing through points m0 and m1 interpolated over t = [0, 1]
        Tangent M[k] = (P[k+1] - P[k-1]) / 2
    */
    public class CatmullRom
    {
        //Struct to keep position, normal and tangent of a spline point
        [System.Serializable]
        public struct CatmullRomPoint
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;

            public CatmullRomPoint(Vector3 position, Quaternion rotation, float fov)
            {
                this.position = position;
                this.rotation = rotation;
                this.fov = fov;
            }
        }

        [System.Serializable]
        public struct PathControlPoint
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
            public int frames;

            public PathControlPoint(Vector3 position, Quaternion rotation, float fov, int frames = 500)
            {
                this.position = position;
                this.rotation = rotation;
                this.fov = fov;
                this.frames = frames;
            }
        }

        private bool closedLoop;

        private CatmullRomPoint[] splinePoints; //Generated spline points

        private PathControlPoint[] controlPoints;
        public bool playingPath;
        int currentPoint = 0;

        //Returns spline points. Count is the summation of all the node frames (it excludes the last node frames if path isn't a closed loop).
        public CatmullRomPoint[] GetPoints()
        {
            if(splinePoints == null)
            {
                throw new System.NullReferenceException("Spline not Initialized!");
            }

            return splinePoints;
        }

        public CatmullRom(PathControlPoint[] controlPoints, bool closedLoop)
        {
            if(controlPoints == null || controlPoints.Length <= 2)
            {
                ExplorerCore.Log(controlPoints);
                throw new ArgumentException("Catmull Rom Error: Too few control points!");
            }

            this.controlPoints = controlPoints;
            this.closedLoop = closedLoop;

            GenerateSplinePoints();
        }

        //Updates control points
        public void Update(PathControlPoint[] controlPoints)
        {
            if(controlPoints.Length <= 0 || controlPoints == null)
            {
                throw new ArgumentException("Invalid control points");
            }

            this.controlPoints = controlPoints;

            GenerateSplinePoints();
        }

        //Updates closed loop values
        public void Update(bool closedLoop)
        {
            this.closedLoop = closedLoop;

            GenerateSplinePoints();
        }

        //Draws a line between every point and the next.
        public void DrawSpline(Color color)
        {
            if(ValidatePoints())
            {

                for(int i = 0; i < splinePoints.Length; i++)
                {
                    if(i == splinePoints.Length - 1 && closedLoop)
                    {
                        Debug.DrawLine(splinePoints[i].position, splinePoints[0].position, color);
                    }                
                    else if(i < splinePoints.Length - 1)
                    {
                        Debug.DrawLine(splinePoints[i].position, splinePoints[i+1].position, color);
                    }
                }
            }
        }

        //Validates if splinePoints have been set already. Throws nullref exception.
        private bool ValidatePoints()
        {
            if(splinePoints == null)
            {
                throw new NullReferenceException("Spline not initialized!");
            }
            return splinePoints != null;
        }

        //Sets the length of the point array based on total amount of frames.
        private void InitializeProperties()
        {
            int pointsToCreate = 0;
            if (closedLoop)
            {
                for (var i = 0; i < controlPoints.Length; i++)
                {
                    pointsToCreate += controlPoints[i].frames;//Loops back to the beggining, so no need to adjust for arrays starting at 0
                }
            }
            else
            {
                for (var i = 0; i < controlPoints.Length - 1; i++)
                {
                    pointsToCreate += controlPoints[i].frames;
                }
            }

            //if(splinePoints != null)
            //    Array.Clear(splinePoints, 0, splinePoints.Length);

            splinePoints = new CatmullRomPoint[pointsToCreate];
        }

        public void MaybeRunPath(){
            if(playingPath){
                if(ExplorerCore.CameraPathsManager != null && currentPoint < splinePoints.Length){
                    CatmullRomPoint point = splinePoints[currentPoint];

                    //ExplorerCore.Log($"velocity: {Vector3.Distance(point.position,FreeCamPanel.ourCamera.transform.position)}/node");

                    FreeCamPanel.ourCamera.transform.position = point.position;
                    FreeCamPanel.ourCamera.transform.rotation = point.rotation;
                    FreeCamPanel.ourCamera.fieldOfView = point.fov;
                    currentPoint++;
                    if(currentPoint >= splinePoints.Length)
                        playingPath = false;
                }
            }
        }

        public void StartPath(){
            currentPoint = 0;
            playingPath = true;
        }

        public void Stop(){
            currentPoint = 0;
            playingPath = false;
        }

        public void Pause(){
            playingPath = false;
        }

        public void Continue(){
            if(currentPoint < ExplorerCore.CameraPathsManager.GetPoints().Length)
                playingPath = true;
        }

        private void GenerateSplinePoints()
        {
            InitializeProperties();

            Vector3 p0, p1, p2, p3; //Previous position, Start position, end position, Next position
            Vector4 r0, r1, r2, r3; //Previous rotation, Start rotation, end rotation, Next rotation
            float fov0, fov1;

            //Fix rotation
            /*
            for(int i = 0; i < controlPoints.Length; i++){
                if(i>0 && Dot(controlPoints[i - 1].rotation, controlPoints[i].rotation) < 0){
                    Quaternion q = controlPoints[i].rotation;
                    controlPoints[i].rotation = new Quaternion(- q.x, - q.y, - q.z, - q.w);
                    ExplorerCore.Log($"cambio orientacion n° {i}");
                }
            }
            */

            // First for loop goes through each individual control point and connects it to the next, so 0-1, 1-2, 2-3 and so on
            int closedAdjustment = closedLoop ? 0 : 1;
            int totalframes = 0;//count the total number of frames so we can save the points in the right splinePoints position
            
            for (int currentPoint = 0; currentPoint < controlPoints.Length - closedAdjustment; currentPoint++)
            {
                bool closedLoopFinalPoint = (closedLoop && currentPoint == controlPoints.Length - 1);

                //Had tu add "controlPoints.Length" because C# mod doesnt work well with negative values
                int previousPoint = closedLoop ? (currentPoint + controlPoints.Length - 1)%(controlPoints.Length) : System.Math.Max(currentPoint - 1, 0);
                int endSegmentPoint = closedLoop ? (currentPoint + 1)%(controlPoints.Length) : System.Math.Min(currentPoint + 1, controlPoints.Length - 1);
                int nextPoint = closedLoop ? (currentPoint + 2)%(controlPoints.Length) : System.Math.Min(currentPoint + 2, controlPoints.Length - 1);

                p0 = controlPoints[previousPoint].position;
                r0 = QuaternionToVector4(controlPoints[previousPoint].rotation);

                p1 = controlPoints[currentPoint].position;
                r1 = QuaternionToVector4(controlPoints[currentPoint].rotation);

                p2 = controlPoints[endSegmentPoint].position;
                r2 = QuaternionToVector4(controlPoints[endSegmentPoint].rotation);

                p3 = controlPoints[nextPoint].position;
                r3 = QuaternionToVector4(controlPoints[nextPoint].rotation);

                //Check if we are using the shortest path on the rotation. If not, change r1 to represent that shortest path.
                if (Vector4.Dot(r0, r1) < 0)
                    r1 = - r1;

                if (Vector4.Dot(r1, r2) < 0)
                    r2 = - r2;

                if (Vector4.Dot(r2, r3) < 0)
                    r3 = - r3;

                fov0 = controlPoints[currentPoint].fov;
                fov1 = controlPoints[endSegmentPoint].fov;

                int frames = controlPoints[currentPoint].frames; //resolution and time the node takes to get to the other node
                float pointStep = 1.0f / frames;

                if ((currentPoint == controlPoints.Length - 2 && !closedLoop) || closedLoopFinalPoint) //Final point
                {
                    pointStep = 1.0f / (frames - 1);  // last point of last segment should reach p1
                }

                // Creates [frames] points between this control point and the next
                for (int tesselatedPoint = 0; tesselatedPoint < frames; tesselatedPoint++)
                {
                    float t = tesselatedPoint * pointStep;

                    CatmullRomPoint point = Evaluate(p0, p1, p2, p3, r0, r1, r2, r3, fov0, fov1, t);

                    splinePoints[totalframes + tesselatedPoint] = point;
                }
                totalframes+=frames;
            }
        }

        static CatmullRomPoint Evaluate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector4 r0, Vector4 r1, Vector4 r2, Vector4 r3, float fovStart, float fovEnd, float t)
        {
            Vector3 position = CatmullRomInterpolation(p0, p1, p2, p3, t);
            Vector4 v4rot = CatmullRomInterpolation(r0, r1, r2, r3, t);
            Quaternion rotation = new Quaternion(v4rot.x, v4rot.y, v4rot.z, v4rot.w);
            float fov = Mathf.SmoothStep(fovStart, fovEnd, t);

            return new CatmullRomPoint(position, rotation, fov);
        }

        //Implementation from: https://qroph.github.io/2018/07/30/smooth-paths-using-catmull-rom-splines.html
        static Vector3 CatmullRomInterpolation(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t){
            float tension = 0;
            float alpha = 0.5f;

            float t01 = (float) System.Math.Pow((double) Vector3.Distance(p0, p1), (double) alpha);
            float t12 = (float) System.Math.Pow((double) Vector3.Distance(p1, p2), (double) alpha);
            float t23 = (float) System.Math.Pow((double) Vector3.Distance(p2, p3), (double) alpha);

            Vector3 v1 = t01 != 0 ? (p1 - p0) / t01 : new Vector3(0, 0, 0);
            Vector3 v2 = t23 != 0 ? (p3 - p2) / t23 : new Vector3(0, 0, 0);
            
            Vector3 m1 = (1.0f - tension) * (p2 - p1 + t12 * (v1 - (p2 -p0) / (t01 + t12)));
            Vector3 m2 = (1.0f - tension) * (p2 - p1 + t12 * (v2 - (p3 -p1) / (t12 + t23)));

            Vector3 a = 2.0f * (p1 - p2) + m1 + m2;
            Vector3 b = -3.0f * (p1 - p2) - m1 - m1 - m2;
            Vector3 c = m1;
            Vector3 d = p1;

            return a * t * t * t + b * t * t + c * t + d;
        }

        static Vector4 CatmullRomInterpolation(Vector4 r0, Vector4 r1, Vector4 r2, Vector4 r3, float t){
            float tension = 0;
            float alpha = 0.5f;

            float t01 = (float) System.Math.Pow((double) Vector4.Distance(r0, r1), (double) alpha);
            float t12 = (float) System.Math.Pow((double) Vector4.Distance(r1, r2), (double) alpha);
            float t23 = (float) System.Math.Pow((double) Vector4.Distance(r2, r3), (double) alpha);

            Vector4 v1 = t01 != 0 ? (r1 - r0) / t01 : new Vector4(0, 0, 0, 0);
            Vector4 v2 = t23 != 0 ? (r3 - r2) / t23 : new Vector4(0, 0, 0, 0);
            
            Vector4 m1 = (1.0f - tension) * (r2 - r1 + t12 * (v1 - (r2 -r0) / (t01 + t12)));
            Vector4 m2 = (1.0f - tension) * (r2 - r1 + t12 * (v2 - (r3 -r1) / (t12 + t23)));

            Vector4 a = 2.0f * (r1 - r2) + m1 + m2;
            Vector4 b = -3.0f * (r1 - r2) - m1 - m1 - m2;
            Vector4 c = m1;
            Vector4 d = r1;

            return a * t * t * t + b * t * t + c * t + d;
            //return Vector4.Lerp(r1, r2, t);
        }

        static Vector3 CatmullRomAlt(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            Vector3 v0 = (p2 - p0) * 0.5f;
            Vector3 v1 = (p3 - p1) * 0.5f;

            float h1 = 2 * t3 - 3 * t2 + 1;
            float h2 = -2 * t3 + 3 * t2;
            float h3 = t3 - 2 * t2 + t;
            float h4 = t3 - t2;

            Vector3 interpolatedPoint = h1 * p1 + h2 * p2 + h3 * v0 + h4 * v1;
            return interpolatedPoint;
        }

        static Vector4 CatmullRomAlt(Vector4 p0, Vector4 p1, Vector4 p2, Vector4 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            Vector4 v0 = (p2 - p0) * 0.5f;
            Vector4 v1 = (p3 - p1) * 0.5f;

            float h1 = 2 * t3 - 3 * t2 + 1;
            float h2 = -2 * t3 + 3 * t2;
            float h3 = t3 - 2 * t2 + t;
            float h4 = t3 - t2;

            Vector4 interpolatedPoint = h1 * p1 + h2 * p2 + h3 * v0 + h4 * v1;
            return interpolatedPoint;
        }

        //Used for calculating path length for constant speed paths
        public float[] GenerateSplinePointsByRes(int resolution = 3000) //resolution of each segment
        {
            List<float> splineSegmentDistances = new List<float>();

            Vector3 p0, p1; //Start point, end point
            Vector3 m0, m1; //Tangents

            // First for loop goes through each individual control point and connects it to the next, so 0-1, 1-2, 2-3 and so on
            int closedAdjustment = closedLoop ? 0 : 1;
            for (int currentPoint = 0; currentPoint < controlPoints.Length - closedAdjustment; currentPoint++)
            {
                bool closedLoopFinalPoint = (closedLoop && currentPoint == controlPoints.Length - 1);

                p0 = controlPoints[currentPoint].position;
                
                if(closedLoopFinalPoint)
                {
                    p1 = controlPoints[0].position;
                }
                else
                {
                    p1 = controlPoints[currentPoint + 1].position;
                }

                // m0
                if (currentPoint == 0) // Tangent M[k] = (P[k+1] - P[k-1]) / 2
                {
                    if(closedLoop)
                    {
                        m0 = p1 - controlPoints[controlPoints.Length - 1].position;
                    }
                    else
                    {
                        m0 = p1 - p0;
                    }
                }
                else
                {
                    m0 = p1 - controlPoints[currentPoint - 1].position;
                }

                // m1
                if (closedLoop)
                {
                    if (currentPoint == controlPoints.Length - 1) //Last point case
                    {
                        m1 = controlPoints[(currentPoint + 2) % controlPoints.Length].position - p0;
                    }
                    else if (currentPoint == 0) //First point case
                    {
                        m1 = controlPoints[currentPoint + 2].position - p0;
                    }
                    else
                    {
                        m1 = controlPoints[(currentPoint + 2) % controlPoints.Length].position - p0;
                    }
                }
                else
                {
                    if (currentPoint < controlPoints.Length - 2)
                    {
                        m1 = controlPoints[(currentPoint + 2) % controlPoints.Length].position - p0;
                    }
                    else
                    {
                        m1 = p1 - p0;
                    }
                }

                m0 *= 0.5f; //Doing this here instead of  in every single above statement
                m1 *= 0.5f;

                float pointStep = 1.0f / resolution;

                if ((currentPoint == controlPoints.Length - 2 && !closedLoop) || closedLoopFinalPoint) //Final point
                {
                    pointStep = 1.0f / (resolution - 1);  // last point of last segment should reach p1
                }

                Vector3 pos1;
                Vector3 pos0 = new Vector3(0, 0, 0); //simply initialization so the compiler doesnt complain.
                float segmentDistance = 0;

                // Creates [resolution] points between this control point and the next
                for (int tesselatedPoint = 1; tesselatedPoint < resolution; tesselatedPoint++)
                {
                    if(tesselatedPoint == 1)
                        pos0 = CalculatePosition(p0, p1, m0, m1, 0);

                    float t = tesselatedPoint * pointStep;
                    pos1 = CalculatePosition(p0, p1, m0, m1, t);
                    segmentDistance += Vector3.Distance(pos0, pos1);

                    pos0 = pos1;
                }

                splineSegmentDistances.Add(segmentDistance);
            }

            return splineSegmentDistances.ToArray();
        }

        //Calculates curve position at t[0, 1]
        public static Vector3 CalculatePosition(Vector3 start, Vector3 end, Vector3 tanPoint1, Vector3 tanPoint2, float t)
        {
            // Hermite curve formula:
            // (2t^3 - 3t^2 + 1) * p0 + (t^3 - 2t^2 + t) * m0 + (-2t^3 + 3t^2) * p1 + (t^3 - t^2) * m1
            Vector3 position = (2.0f * t * t * t - 3.0f * t * t + 1.0f) * start
                + (t * t * t - 2.0f * t * t + t) * tanPoint1
                + (-2.0f * t * t * t + 3.0f * t * t) * end
                + (t * t * t - t * t) * tanPoint2;

            return position;
        }

        static Vector4 QuaternionToVector4(Quaternion q){
            return new Vector4(q.x, q.y, q.z, q.w);
        }
    }
}
