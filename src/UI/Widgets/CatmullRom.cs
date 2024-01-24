using UnityExplorer.UI.Panels;

using System;
using System.Threading;

#if UNHOLLOWER
using UnhollowerRuntimeLib;
#endif
#if INTEROP
using Il2CppInterop.Runtime.Injection;
#endif

namespace UnityExplorer.CatmullRom
{
    // Struct to keep position, rotation and fov of a spline point
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

        public CatmullRomPoint(Vector3 position, Vector4 rotation, float fov)
        {
            this.position = position;
            this.rotation = Vector4ToQuaternion(rotation);
            this.fov = fov;
        }

        public static Vector4 QuaternionToVector4(Quaternion q){
            return new Vector4(q.x, q.y, q.z, q.w);
        }

        public static Quaternion Vector4ToQuaternion(Vector4 v){
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        public static CatmullRomPoint operator +(CatmullRomPoint a, CatmullRomPoint b)
        {
            Vector4 ra = QuaternionToVector4(a.rotation);
            Vector4 rb = QuaternionToVector4(b.rotation);

            Vector4 newRot = ra + rb;
            return new CatmullRomPoint(a.position + b.position, Vector4ToQuaternion(newRot), a.fov + b.fov); // what to do with the fov??
        }

        public static CatmullRomPoint operator -(CatmullRomPoint a, CatmullRomPoint b)
        {
            Vector4 newRot = QuaternionToVector4(a.rotation) - QuaternionToVector4(b.rotation);
            return new CatmullRomPoint(a.position - b.position, Vector4ToQuaternion(newRot), a.fov - b.fov); // what to do with the fov??
        }

        public static CatmullRomPoint operator /(CatmullRomPoint a, float b)
        {
            //Vector4 newRot = QuaternionToVector4(a.rotation)/b;
            //return new CatmullRomPoint(a.position/b, Vector4ToQuaternion(newRot), a.fov / b); // what to do with the fov??
            return new CatmullRomPoint(a.position/b, a.rotation, a.fov);
        }

        public static CatmullRomPoint operator *(CatmullRomPoint a, float b)
        {
            //Vector4 newRot = QuaternionToVector4(a.rotation)*b;
            //return new CatmullRomPoint(a.position*b, Vector4ToQuaternion(newRot), a.fov * b); // what to do with the fov??
            return new CatmullRomPoint(a.position*b, a.rotation, a.fov); // what to do with the fov??
        }

        public CatmullRomPoint Normalize()
        {
            position.Normalize();
            return this;
        }
    }

    public class CatmullRomMover : MonoBehaviour
    {
#if CPP
        static CatmullRomMover()
        {
            ClassInjector.RegisterTypeInIl2Cpp<CatmullRomMover>();
        }

        public CatmullRomMover(IntPtr ptr) : base(ptr) { }
#endif

        bool playingPath;
        private bool closedLoop;
        private CatmullRomPoint[] splinePoints;
        // The point the camera will be following along the path
        private CatmullRomPoint lookahead;
        // How smooth the path is (beware of not making too many calculations per update tho)
        float lookaheadDelta;
        float speed;
        // Current position in the path, from 0 to 1
        float delta;

        public CatmullRomMover(){
            splinePoints = new CatmullRomPoint[] { };
            speed = 10;
            lookaheadDelta = 0.01f;
            delta = lookaheadDelta;
        }

        public void StartPath(){
            if(splinePoints == null || splinePoints.Length <= 2)
            {
                ExplorerCore.Log(splinePoints);
                throw new ArgumentException("Catmull Rom Error: Too few spline points!");
            }
            else {
                playingPath = true;
                MoveCameraToPoint(splinePoints[0]);
                lookahead = GetPointFromPath(delta);
            }
        }

        public void TogglePause(){
            playingPath = !playingPath;
        }

        public void Stop(){
            playingPath = false;
            delta = lookaheadDelta;
            lookahead = GetPointFromPath(delta);
            MoveCameraToPoint(splinePoints[0]);
        }

        public void setClosedLoop(bool newClosedLoop){
            closedLoop = newClosedLoop;
            ExplorerCore.LogWarning("Update closedLoop!");
        }

        public void setSplinePoints(CatmullRomPoint[] newSplinePoints){
            splinePoints = newSplinePoints;
            ExplorerCore.LogWarning("Update spline points!");
        }

        public void setSpeed(float newSpeed){
            speed = newSpeed;
            lookaheadDelta = newSpeed / 1000;
        }

        public void Update(){
            if (delta >= 1 && FreeCamPanel.ourCamera != null){
                playingPath = false;
                delta = lookaheadDelta;
            }
            if (playingPath) AdvanceMover(Time.deltaTime);
        }

        /*
        private CatmullRomPoint GetPointFromPath(float d){
            //ExplorerCore.LogWarning(splinePoints.Length);
            int currentSegment = (int)(splinePoints.Length * d); // have to do + 1 if closed loop
            //ExplorerCore.LogWarning(currentSegment);
            CatmullRomPoint lastPointInPath = splinePoints[Math.Min(currentSegment, splinePoints.Length - 1)];
            CatmullRomPoint nextPointInPath = splinePoints[Math.Min(currentSegment + 1, splinePoints.Length - 1)];

            float interpolationBetweenPaths = (d - (float)currentSegment / (float)splinePoints.Length) * (float)splinePoints.Length;
            // Dumb Lerp to test things. Should use proper CatmullRom instead.
            return lastPointInPath * (1 - interpolationBetweenPaths) + nextPointInPath * interpolationBetweenPaths;
        }
        */

        private CatmullRomPoint GetCurrentPoint(){
            return new CatmullRomPoint(FreeCamPanel.ourCamera.transform.position, FreeCamPanel.ourCamera.transform.rotation, FreeCamPanel.ourCamera.fieldOfView);
        }

        private void AdvanceMover(float dt){
            // We use 0.01665f (60fps) in place of Time.DeltaTime so the camera moves at fullspeed even in slow motion.
            float move = 0.01665f * speed; // units to move
            while (move > 0.0) {
                CatmullRomPoint currentPoint = GetCurrentPoint();
                // distance between target and current position
                float room = Vector3.Distance(lookahead.position, currentPoint.position); 

                // how much we're actually moving this iteration
                // move the whole distance to the lookahead, or would the move cut us short
                float actual = Math.Min(move, room);

                MoveCameraStep(currentPoint, actual, room);

                // update move to be the remaining amount we need to move
                move -= actual;
                //ExplorerCore.LogWarning($"move: {move}, actual: {actual}, room: {room}, delta:{delta}");
                currentPoint = GetCurrentPoint();
                if(delta > 1) break;
                //ExplorerCore.LogWarning($"currentPoint.position: {currentPoint.position}, currentPoint.rotation: {currentPoint.rotation}");

                // update room to check if we need a new lookahead
                room = Vector3.Distance(lookahead.position, currentPoint.position); 
                // while ends, need another lookaheadDelta for the next Update
                if (room <= 0) {
                    delta += lookaheadDelta;
                    lookahead = GetPointFromPath(delta);
                }
            }
        }

        public void MoveCameraStep(CatmullRomPoint currentPoint, float actual, float room){
            // move my position accordingly
            //CatmullRomPoint direction = ((lookahead - currentPoint) / room)*actual;

            CatmullRomPoint direction = (lookahead - currentPoint).Normalize() * actual;
            //float rotationDiff = Vector4.Distance(CatmullRomPoint.QuaternionToVector4(lookahead.rotation), CatmullRomPoint.QuaternionToVector4(currentPoint.rotation));
            //ExplorerCore.LogWarning($"direction.rotation: {direction.rotation}, rotation diff: {rotationDiff}");

            Vector4 ra = CatmullRomPoint.QuaternionToVector4(currentPoint.rotation);
            Vector4 rb = CatmullRomPoint.QuaternionToVector4(direction.rotation) * actual / room;

            if (Vector4.Dot(ra, rb) < 0)
            {
                //ExplorerCore.LogWarning($"tengo que arreglar quaternion");
                rb = - rb;
            }
            FreeCamPanel.ourCamera.transform.position += direction.position;
            FreeCamPanel.ourCamera.transform.rotation = CatmullRomPoint.Vector4ToQuaternion(ra + rb);
            //FreeCamPanel.ourCamera.transform.rotation = lookahead.rotation;
            FreeCamPanel.ourCamera.fieldOfView += direction.fov * actual / room;
            //ExplorerCore.LogWarning($"current cam pos: {FreeCamPanel.ourCamera.transform.position} at d: {delta}");
        }

        public void MoveCameraToPoint(CatmullRomPoint newPoint){
            FreeCamPanel.ourCamera.transform.position = newPoint.position;
            FreeCamPanel.ourCamera.transform.rotation = newPoint.rotation;
            FreeCamPanel.ourCamera.fieldOfView = newPoint.fov;
        }

        /*test*/
        private CatmullRomPoint GetPointFromPath(float d)
        {
            Vector3 p0, p1, p2, p3; //Previous position, Start position, end position, Next position
            Vector4 r0, r1, r2, r3; //Previous rotation, Start rotation, end rotation, Next rotation
            float fov0, fov1;

            //Fix rotation
            /*
            for(int i = 0; i < splinePoints.Length; i++){
                if(i>0 && Dot(splinePoints[i - 1].rotation, splinePoints[i].rotation) < 0){
                    Quaternion q = splinePoints[i].rotation;
                    splinePoints[i].rotation = new Quaternion(- q.x, - q.y, - q.z, - q.w);
                    ExplorerCore.Log($"cambio orientacion n° {i}");
                }
            }
            */
            
            // First for loop goes through each individual control point and connects it to the next, so 0-1, 1-2, 2-3 and so on
            int closedAdjustment = closedLoop ? 0 : 1;

            int currentPoint = (int)((splinePoints.Length - closedAdjustment) * d); // have to do + 1 if closed loop

            //ExplorerCore.LogWarning($"currentPoint: {currentPoint}, d: {d}");
            
            bool closedLoopFinalPoint = (closedLoop && currentPoint == splinePoints.Length - 1);

            //Had tu add "splinePoints.Length" because C# mod doesnt work well with negative values
            int previousPoint = closedLoop ? (currentPoint + splinePoints.Length - 1)%(splinePoints.Length) : System.Math.Max(currentPoint - 1, 0);
            int endSegmentPoint = closedLoop ? (currentPoint + 1)%(splinePoints.Length) : System.Math.Min(currentPoint + 1, splinePoints.Length - 1);
            int nextPoint = closedLoop ? (currentPoint + 2)%(splinePoints.Length) : System.Math.Min(currentPoint + 2, splinePoints.Length - 1);

            ExplorerCore.LogWarning($"p0: {previousPoint}, p1: {currentPoint}, p2: {endSegmentPoint}, p3: {nextPoint},");

            p0 = splinePoints[previousPoint].position;
            r0 = CatmullRomPoint.QuaternionToVector4(splinePoints[previousPoint].rotation);

            p1 = splinePoints[currentPoint].position;
            r1 = CatmullRomPoint.QuaternionToVector4(splinePoints[currentPoint].rotation);

            p2 = splinePoints[endSegmentPoint].position;
            r2 = CatmullRomPoint.QuaternionToVector4(splinePoints[endSegmentPoint].rotation);

            p3 = splinePoints[nextPoint].position;
            r3 = CatmullRomPoint.QuaternionToVector4(splinePoints[nextPoint].rotation);

            if (d >= 1) return splinePoints[currentPoint];

            //Check if we are using the shortest path on the rotation. If not, change r1 to represent that shortest path.
            if (Vector4.Dot(r0, r1) < 0)
                r1 = - r1;

            if (Vector4.Dot(r1, r2) < 0)
                r2 = - r2;

            if (Vector4.Dot(r2, r3) < 0)
                r3 = - r3;

            fov0 = splinePoints[currentPoint].fov;
            fov1 = splinePoints[endSegmentPoint].fov;

            //float t = (d - (float)currentPoint / (float)(splinePoints.Length + closedAdjustment)) * (float)(splinePoints.Length + closedAdjustment);
            float t = ((splinePoints.Length - closedAdjustment) * d) % 1;

            //ExplorerCore.LogWarning($"t: {t}, d: {d}");

            CatmullRomPoint newPoint = Evaluate(p0, p1, p2, p3, r0, r1, r2, r3, fov0, fov1, t);

            //ExplorerCore.LogWarning($"p1.pos: {p1}, p2.pos: {p2}, t: {t}, dist-p1: {Vector3.Distance(GetCurrentPoint().position, p1)}, dist-p2: {Vector3.Distance(GetCurrentPoint().position, p2)}");

            return newPoint;
        }

        private CatmullRomPoint Evaluate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector4 r0, Vector4 r1, Vector4 r2, Vector4 r3, float fovStart, float fovEnd, float t)
        {
            Vector3 position = CatmullRomInterpolation(p0, p1, p2, p3, t);
            Vector4 v4rot = CatmullRomInterpolation(r0, r1, r2, r3, t);
            Quaternion rotation = new Quaternion(v4rot.x, v4rot.y, v4rot.z, v4rot.w);
            float fov = Mathf.SmoothStep(fovStart, fovEnd, t);

            return new CatmullRomPoint(position, rotation, fov);
        }

        //Implementation from: https://qroph.github.io/2018/07/30/smooth-paths-using-catmull-rom-splines.html
        private Vector3 CatmullRomInterpolation(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t){
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

        private Vector4 CatmullRomInterpolation(Vector4 r0, Vector4 r1, Vector4 r2, Vector4 r3, float t){
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
    }
    
}
