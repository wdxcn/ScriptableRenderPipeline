using UnityEditor.AnimatedValues;
using UnityEngine;

namespace UnityEditor.Rendering.LookDev
{
    public class LookDevContext : ScriptableObject
    {
        public LayoutContext layout { get; private set; } = new LayoutContext();
        public ViewContext viewA { get; private set; } = new ViewContext();
        public ViewContext viewB { get; private set; } = new ViewContext();
        public CameraState cameraA { get; private set; } = new CameraState();
        public CameraState cameraB { get; private set; } = new CameraState();
    }

    public class LayoutContext
    {
        public enum Layout { FullA, FullB, HorizontalSplit, VerticalSplit, CustomSplit, CustomCircular }

        public Layout viewLayout;
        public bool showHDRI;

        //[TODO: add tool position]

        public bool isSimpleView => viewLayout == Layout.FullA || viewLayout == Layout.FullB;
        public bool isMultiView => viewLayout == Layout.HorizontalSplit || viewLayout == Layout.VerticalSplit;
        public bool isCombinedView => viewLayout == Layout.CustomSplit || viewLayout == Layout.CustomCircular;
    }

    public class ViewContext
    {
        //[TODO: add object]
        //[TODO: add object position]
        //[TODO: add camera frustum]
        //[TODO: add HDRI]
        //[TODO: manage shadow and lights]
    }
    
    public class CameraState
    {
        private static readonly Quaternion kDefaultRotation = Quaternion.LookRotation(new Vector3(0.0f, 0.0f, 1.0f));
        private const float kDefaultViewSize = 10f;
        private static readonly Vector3 kDefaultPivot = Vector3.zero;
        private const float kDefaultFoV = 90f;
        private static readonly float distanceCoef = 1f / Mathf.Tan(kDefaultFoV * 0.5f * Mathf.Deg2Rad);

        //Note: we need animation to do the same focus as in SceneView
        [SerializeField] private AnimVector3 m_Pivot = new AnimVector3(kDefaultPivot);
        [SerializeField] private AnimQuaternion m_Rotation = new AnimQuaternion(kDefaultRotation);
        [SerializeField] private AnimFloat m_ViewSize = new AnimFloat(kDefaultViewSize);

        public AnimVector3 pivot { get { return m_Pivot; } set { m_Pivot = value; } }
        public AnimQuaternion rotation { get { return m_Rotation; } set { m_Rotation = value; } }
        public AnimFloat viewSize { get { return m_ViewSize; } set { m_ViewSize = value; } }

        public float cameraDistance => m_ViewSize.value * distanceCoef;
        
        public void UpdateCamera(Camera camera)
        {
            camera.transform.rotation = m_Rotation.value;
            camera.transform.position = m_Pivot.value + camera.transform.rotation * new Vector3(0, 0, -cameraDistance);

            float farClip = Mathf.Max(1000f, 2000f * m_ViewSize.value);
            camera.nearClipPlane = farClip * 0.000005f;
            camera.farClipPlane = farClip;
        }
    }
}
