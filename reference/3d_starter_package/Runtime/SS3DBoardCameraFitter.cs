using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Fixed orthographic framing over the entire board, no player panning/zoom.
    /// Angle changes only the rendering; gameplay remains 2D XZ node logic.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SS3DBoardCameraFitter : MonoBehaviour
    {
        [Range(35f,89f)]public float downwardAngle=63f;
        [Range(.45f,.97f)]public float viewportUsage=.80f;
        public float minimumSize=2.4f;
        public float cameraDistance=18f;
        public float additionalWorldPadding=.5f;

        public void Fit(Bounds board,Transform boardTransform)
        {
            var camera=GetComponent<Camera>();camera.orthographic=true;
            camera.transform.rotation=Quaternion.Euler(downwardAngle,0,0);
            var center=board.center;
            var inv=Quaternion.Inverse(camera.transform.rotation);
            float maxX=0,maxY=0;
            for(int i=0;i<8;i++)
            {
                var corner=new Vector3(
                    center.x+board.extents.x*((i&1)==0?-1:1),
                    center.y+board.extents.y*((i&2)==0?-1:1),
                    center.z+board.extents.z*((i&4)==0?-1:1));
                Vector3 projected=inv*(corner-center);
                maxX=Mathf.Max(maxX,Mathf.Abs(projected.x));
                maxY=Mathf.Max(maxY,Mathf.Abs(projected.y));
            }
            float aspect=Mathf.Max(.4f,camera.aspect);
            camera.orthographicSize=Mathf.Max(minimumSize,
                Mathf.Max((maxY+additionalWorldPadding),(maxX+additionalWorldPadding)/aspect) / viewportUsage);
            camera.transform.position=center-camera.transform.forward*cameraDistance;
            camera.nearClipPlane=.1f;camera.farClipPlane=cameraDistance+50f;
        }
    }
}
