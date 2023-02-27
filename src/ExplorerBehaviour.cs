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
            public Vector3 tangent;
            public Vector3 normal;
            public float fov;

            public CatmullRomPoint(Vector3 position, Quaternion rotation, Vector3 tangent, Vector3 normal, float fov)
            {
                this.position = position;
                this.rotation = rotation;
                this.tangent = tangent;
                this.normal = normal;
                this.fov = fov;
            }
        }

        [System.Serializable]
        public struct PathControlPoint
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;

            public PathControlPoint(Vector3 position, Quaternion rotation, float fov)
            {
                this.position = position;
                this.rotation = rotation;
                this.fov = fov;
            }
        }

        private int resolution; //Amount of points between control points. [Tesselation factor]
        private bool closedLoop;

        private CatmullRomPoint[] splinePoints; //Generated spline points

        private PathControlPoint[] controlPoints;
        public bool playingPath;
        int currentPoint = 0;

        //Returns spline points. Count is contorolPoints * resolution + [resolution] points if closed loop.
        public CatmullRomPoint[] GetPoints()
        {
            if(splinePoints == null)
            {
                throw new System.NullReferenceException("Spline not Initialized!");
            }

            return splinePoints;
        }

        public CatmullRom(PathControlPoint[] controlPoints, int resolution, bool closedLoop)
        {
            if(controlPoints == null || controlPoints.Length <= 2 || resolution < 2)
            {
                ExplorerCore.Log(controlPoints);
                throw new ArgumentException("Catmull Rom Error: Too few control points or resolution too small");
            }

            this.controlPoints = controlPoints;
            this.resolution = resolution;
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

        //Updates resolution and closed loop values
        public void Update(int resolution, bool closedLoop)
        {
            if(resolution < 2)
            {
                throw new ArgumentException("Invalid Resolution. Make sure it's >= 1");
            }
            this.resolution = resolution;
            this.closedLoop = closedLoop;

            GenerateSplinePoints();
        }

        //Draws a line between every point and the next.
        public void DrawSpline(Color color)
        {
            if(ValidatePoints())
            {
                //LineRenderer lineRenderer = new GameObject().GetComponent<LineRenderer>();

                for(int i = 0; i < splinePoints.Length; i++)
                {
                    if(i == splinePoints.Length - 1 && closedLoop)
                    {
                        //lineRenderer.SetPosition(i, splinePoints[0].position);
                        //lineRenderer.SetPosition(i + 1, splinePoints[i].position);

                        Debug.DrawLine(splinePoints[i].position, splinePoints[0].position, color);
                    }                
                    else if(i < splinePoints.Length - 1)
                    {
                        //lineRenderer.SetPosition(i, splinePoints[i].position);
                        //lineRenderer.SetPosition(i + 1, splinePoints[i + 1].position);

                        Debug.DrawLine(splinePoints[i].position, splinePoints[i+1].position, color);
                    }
                }
            }
        }

        public void DrawNormals(float extrusion, Color color)
        {
            if(ValidatePoints())
            {
                for(int i = 0; i < splinePoints.Length; i++)
                {
                    Debug.DrawLine(splinePoints[i].position, splinePoints[i].position + splinePoints[i].normal * extrusion, color);
                }
            }
        }

        public void DrawTangents(float extrusion, Color color)
        {
            if(ValidatePoints())
            {
                for(int i = 0; i < splinePoints.Length; i++)
                {
                    Debug.DrawLine(splinePoints[i].position, splinePoints[i].position + splinePoints[i].tangent * extrusion, color);
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

        //Sets the length of the point array based on resolution/closed loop.
        private void InitializeProperties()
        {
            int pointsToCreate;
            if (closedLoop)
            {
                pointsToCreate = resolution * controlPoints.Length; //Loops back to the beggining, so no need to adjust for arrays starting at 0
            }
            else
            {
                pointsToCreate = resolution * (controlPoints.Length - 1);
            }

            splinePoints = new CatmullRomPoint[pointsToCreate];       
        }

        //Math stuff to generate the spline points
        private void GenerateSplinePoints()
        {
            InitializeProperties();

            Vector3 p0, p1; //Start point, end point
            Quaternion r0, r1; //Start rotation, end rotation
            Vector3 m0, m1; //Tangents
            float fov0, fov1;

            // First for loop goes through each individual control point and connects it to the next, so 0-1, 1-2, 2-3 and so on
            int closedAdjustment = closedLoop ? 0 : 1;
            for (int currentPoint = 0; currentPoint < controlPoints.Length - closedAdjustment; currentPoint++)
            {
                bool closedLoopFinalPoint = (closedLoop && currentPoint == controlPoints.Length - 1);

                p0 = controlPoints[currentPoint].position;
                r0 = controlPoints[currentPoint].rotation;
                fov0 = controlPoints[currentPoint].fov;
                
                if(closedLoopFinalPoint)
                {
                    p1 = controlPoints[0].position;
                    r1 = controlPoints[0].rotation;
                    fov1 = controlPoints[0].fov;
                }
                else
                {
                    p1 = controlPoints[currentPoint + 1].position;
                    r1 = controlPoints[currentPoint + 1].rotation;
                    fov1 = controlPoints[currentPoint + 1].fov;
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

                // Creates [resolution] points between this control point and the next
                for (int tesselatedPoint = 0; tesselatedPoint < resolution; tesselatedPoint++)
                {
                    float t = tesselatedPoint * pointStep;

                    CatmullRomPoint point = Evaluate(p0, p1, r0, r1, m0, m1, fov0, fov1, t);

                    splinePoints[currentPoint * resolution + tesselatedPoint] = point;
                }
            }
        }

        //Evaluates curve at t[0, 1]. Returns point/rotation/normal/tan struct. [0, 1] means clamped between 0 and 1.
        public static CatmullRomPoint Evaluate(Vector3 posStart, Vector3 posEnd, Quaternion r0, Quaternion r1, Vector3 tanPoint1, Vector3 tanPoint2, float fovStart, float fovEnd, float t)
        {
            Vector3 position = CalculatePosition(posStart, posEnd, tanPoint1, tanPoint2, t);
            Quaternion rotation = Quaternion.Slerp(r0, r1, t);
            Vector3 tangent = CalculateTangent(posStart, posEnd, tanPoint1, tanPoint2, t);            
            Vector3 normal = NormalFromTangent(tangent);
            float fov = Mathf.SmoothStep(fovStart, fovEnd, t);

            return new CatmullRomPoint(position, rotation, tangent, normal, fov);
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

        //Calculates tangent at t[0, 1]
        public static Vector3 CalculateTangent(Vector3 start, Vector3 end, Vector3 tanPoint1, Vector3 tanPoint2, float t)
        {
            // Calculate tangents
            // p'(t) = (6t² - 6t)p0 + (3t² - 4t + 1)m0 + (-6t² + 6t)p1 + (3t² - 2t)m1
            Vector3 tangent = (6 * t * t - 6 * t) * start
                + (3 * t * t - 4 * t + 1) * tanPoint1
                + (-6 * t * t + 6 * t) * end
                + (3 * t * t - 2 * t) * tanPoint2;

            return tangent.normalized;
        }
        
        //Calculates normal vector from tangent
        public static Vector3 NormalFromTangent(Vector3 tangent)
        {
            return Vector3.Cross(tangent, Vector3.up).normalized / 2;
        }

        public void MaybeRunPath(){
            if(playingPath){
                if(ExplorerCore.CameraPathsManager != null && currentPoint < ExplorerCore.CameraPathsManager.GetPoints().Length){
                    CatmullRomPoint point = ExplorerCore.CameraPathsManager.GetPoints()[currentPoint];
                    FreeCamPanel.ourCamera.transform.position = point.position;
                    FreeCamPanel.ourCamera.transform.rotation = point.rotation;
                    FreeCamPanel.ourCamera.fieldOfView = point.fov;
                    currentPoint++;
                    if(currentPoint >= ExplorerCore.CameraPathsManager.GetPoints().Length)
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
    }

    /*

    public struct LineDrawer
    {
        private LineRenderer lineRenderer;
        private float lineSize;

        public LineDrawer(float lineSize = 0.2f)
        {
            //GameObject lineObj = new GameObject("LineObj");
            //lineRenderer = lineObj.AddComponent<UnityEngine.LineRenderer>();

            //Particles/Additive
            //lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));

            lineRenderer = new GameObject().GetComponent<UnityEngine.LineRenderer>();

            this.lineSize = lineSize;
        }

        private void init(float lineSize = 0.2f)
        {
            if (lineRenderer == null)
            {
                //GameObject lineObj = new GameObject("LineObj");
                //lineRenderer = lineObj.AddComponent<UnityEngine.LineRenderer>();
                
                //Particles/Additive
                //lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));

                lineRenderer = new GameObject().GetComponent<UnityEngine.LineRenderer>();

                this.lineSize = lineSize;
            }
        }

        //Draws lines through the provided vertices
        public void DrawLineInGameView(Vector3 start, Vector3 end, Color color)
        {
            if (lineRenderer == null)
            {
                init(0.2f);
            }

            //Set color
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            //Set width
            lineRenderer.startWidth = lineSize;
            lineRenderer.endWidth = lineSize;

            //Set line count which is 2
            lineRenderer.positionCount = 2;

            //Set the postion of both two lines
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        public void Destroy()
        {
            if (lineRenderer != null)
            {
                //UnityEngine.Object.Destroy(lineRenderer.gameObject);
            }
        }
    }
    */
}
