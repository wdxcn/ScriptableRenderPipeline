using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    using AntialiasingMode = HDAdditionalCameraData.AntialiasingMode;

    // This holds all the matrix data we need for rendering, including data from the previous frame
    // (which is the main reason why we need to keep them around for a minimum of one frame).
    // HDCameras are automatically created & updated from a source camera and will be destroyed if
    // not used during a frame.
    public class HDCamera
    {
        // Do we need all of them ?
        public Matrix4x4[] multiviewMatrixView;
        public Matrix4x4[] multiviewMatrixViewProj;
        public Matrix4x4[] multiviewMatrixViewProjNonJittered;
        public Matrix4x4[] multiviewMatrixViewProjPrev;
        public Matrix4x4[] multiviewMatrixViewProjPrevNoCameraTrans;
        public Matrix4x4[] multiviewMatrixProj;
        public Matrix4x4[] multiviewMatrixProjNonJittered;
        public Matrix4x4[] multiviewMatrixInvProj;
        public Matrix4x4[] multiviewMatrixInvView;
        public Matrix4x4[] multiviewMatrixInvViewProj;

                //Vector4[] worldSpaceCameraPosStereoEyeOffset;

        public Vector4[] multiviewCameraPos;
        public Vector4[] multiviewCameraPosPrev;

        public Vector4   screenSize;
        public Frustum   frustum;
        public Vector4[] frustumPlaneEquations;
        public Camera    camera;
        public Vector4   taaJitter;
        public int       taaFrameIndex;
        public Vector2   taaFrameRotation;
        public Vector4   zBufferParams;
        public Vector4   unity_OrthoParams;
        public Vector4   projectionParams;
        public Vector4   screenParams;
        public int       volumeLayerMask;
        public Transform volumeAnchor;
        // This will have the correct viewport position and the size will be full resolution (ie : not taking dynamic rez into account)
        public Rect      finalViewport;

        public bool colorPyramidHistoryIsValid = false;
        public bool volumetricHistoryIsValid   = false; // Contains garbage otherwise
        public int  colorPyramidHistoryMipCount = 0;
        public VolumetricLightingSystem.VBufferParameters[] vBufferParams; // Double-buffered

        // XR multipass and multiview rendering
        // TODO: rename to m_M
        int m_multipassIndex = 0;
        int m_multiviewCount = 1;

        public int multipassIndex { get { return m_multipassIndex; } }
        public int multiviewCount { get { return m_multiviewCount; } }
        public bool multipassEnabled { get { return camera.stereoEnabled && multipassIndex > 0; } }
        public bool multiviewEnabled { get { return m_multiviewCount > 1; } }
        public Camera.StereoscopicEye legacyMultipassEyeIndex { get { return (Camera.StereoscopicEye)(m_multipassIndex - 1); } }

        // Recorder specific
        IEnumerator<Action<RenderTargetIdentifier, CommandBuffer>> m_RecorderCaptureActions;
        int m_RecorderTempRT = Shader.PropertyToID("TempRecorder");
        MaterialPropertyBlock m_RecorderPropertyBlock = new MaterialPropertyBlock();

        // Non oblique projection matrix (RHS)
        public Matrix4x4 nonObliqueProjMatrix
        {
            get
            {
                return m_AdditionalCameraData != null
                    ? m_AdditionalCameraData.GetNonObliqueProjection(camera)
                    : GeometryUtils.CalculateProjectionMatrix(camera);
            }
        }

        // This is the viewport size actually used for this camera (as it can be altered by VR for example)
        int m_ActualWidth;
        int m_ActualHeight;
        // And for the previous frame...
        Vector2Int m_ViewportSizePrevFrame;

        // This is the scale of the camera viewport compared to the reference size of our Render Targets (RTHandle.maxSize)
        Vector2 m_ViewportScaleCurrentFrame;
        Vector2 m_ViewportScalePreviousFrame;
        // Current mssa sample
        MSAASamples m_msaaSamples;
        FrameSettings m_frameSettings;

        public int actualWidth { get { return m_ActualWidth; } }
        public int actualHeight { get { return m_ActualHeight; } }
        public Vector2 viewportScale { get { return m_ViewportScaleCurrentFrame; } }
        public Vector2Int viewportSizePrevFrame { get { return m_ViewportSizePrevFrame; } }
        public Vector4 doubleBufferedViewportScale {
            get
            {
                if (HDDynamicResolutionHandler.instance.HardwareDynamicResIsEnabled())
                {
                    return new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                }

                return new Vector4(m_ViewportScaleCurrentFrame.x, m_ViewportScaleCurrentFrame.y, m_ViewportScalePreviousFrame.x, m_ViewportScalePreviousFrame.y);
            }
        }
        public MSAASamples msaaSamples { get { return m_msaaSamples; } }

        public FrameSettings frameSettings { get { return m_frameSettings; } }

        // Always true for cameras that just got added to the pool - needed for previous matrices to
        // avoid one-frame jumps/hiccups with temporal effects (motion blur, TAA...)
        public bool isFirstFrame { get; private set; }

        // Ref: An Efficient Depth Linearization Method for Oblique View Frustums, Eq. 6.
        // TODO: pass this as "_ZBufferParams" if the projection matrix is oblique.
        public Vector4 invProjParam
        {
            get
            {
                Debug.Assert(!multiviewEnabled);
                var p = multiviewMatrixProj[0];
                return new Vector4(
                    p.m20 / (p.m00 * p.m23),
                    p.m21 / (p.m11 * p.m23),
                    -1f / p.m23,
                    (-p.m22 + p.m20 * p.m02 / p.m00 + p.m21 * p.m12 / p.m11) / p.m23
                    );
            }
        }

        public bool isMainGameView { get { return camera.cameraType == CameraType.Game && camera.targetTexture == null; } }

        // The only way to reliably keep track of a frame change right now is to compare the frame
        // count Unity gives us. We need this as a single camera could be rendered several times per
        // frame and some matrices only have to be computed once. Realistically this shouldn't
        // happen, but you never know...
        int m_LastFrameActive;

        public bool clearDepth
        {
            get { return m_AdditionalCameraData != null ? m_AdditionalCameraData.clearDepth : camera.clearFlags != CameraClearFlags.Nothing; }
        }

        public HDAdditionalCameraData.ClearColorMode clearColorMode
        {
            get
            {
                if (m_AdditionalCameraData != null)
                {
                    return m_AdditionalCameraData.clearColorMode;
                }

                if (camera.clearFlags == CameraClearFlags.Skybox)
                    return HDAdditionalCameraData.ClearColorMode.Sky;
                else if (camera.clearFlags == CameraClearFlags.SolidColor)
                    return HDAdditionalCameraData.ClearColorMode.Color;
                else // None
                    return HDAdditionalCameraData.ClearColorMode.None;
            }
        }

        public Color backgroundColorHDR
        {
            get
            {
                if (m_AdditionalCameraData != null)
                {
                    return m_AdditionalCameraData.backgroundColorHDR;
                }

                // The scene view has no additional data so this will correctly pick the editor preference backround color here.
                return camera.backgroundColor.linear;
            }
        }

        public HDAdditionalCameraData.FlipYMode flipYMode
        {
            get
            {
                if (m_AdditionalCameraData != null)
                    return m_AdditionalCameraData.flipYMode;
                return HDAdditionalCameraData.FlipYMode.Automatic;
            }
        }

        // This value will always be correct for the current camera, no need to check for
        // game view / scene view / preview in the editor, it's handled automatically
        public AntialiasingMode antialiasing { get; private set; } = AntialiasingMode.None;

        public HDAdditionalCameraData.SMAAQualityLevel SMAAQuality { get; private set; } = HDAdditionalCameraData.SMAAQualityLevel.Medium;


        public bool dithering => m_AdditionalCameraData != null && m_AdditionalCameraData.dithering;

        public bool stopNaNs => m_AdditionalCameraData != null && m_AdditionalCameraData.stopNaNs;

        public HDPhysicalCamera physicalParameters => m_AdditionalCameraData?.physicalParameters;

        public bool invertFaceCulling
            => m_AdditionalCameraData != null && m_AdditionalCameraData.invertFaceCulling;

        public LayerMask probeLayerMask
            => m_AdditionalCameraData != null
            ? m_AdditionalCameraData.probeLayerMask
            : (LayerMask)~0;

        static Dictionary<Tuple<Camera, int>, HDCamera> s_Cameras = new Dictionary<Tuple<Camera, int>, HDCamera>();
        static List<Tuple<Camera, int>> s_Cleanup = new List<Tuple<Camera, int>>(); // Recycled to reduce GC pressure

        HDAdditionalCameraData m_AdditionalCameraData;

        BufferedRTHandleSystem m_HistoryRTSystem = new BufferedRTHandleSystem();

        int numColorPyramidBuffersAllocated = 0;
        int numVolumetricBuffersAllocated   = 0;

        public HDCamera(Camera cam, int passIndex)
        {
            camera = cam;
            m_multipassIndex = passIndex;

            if (cam.stereoEnabled && XRGraphics.stereoRenderingMode == XRGraphics.StereoRenderingMode.SinglePassInstanced)
                m_multiviewCount = 2;

            frustum = new Frustum();
            frustum.planes = new Plane[6];
            frustum.corners = new Vector3[8];
            frustumPlaneEquations = new Vector4[6];

            m_AdditionalCameraData = null; // Init in Update

            Reset();
        }

        public bool IsTAAEnabled()
        {
            return antialiasing == AntialiasingMode.TemporalAntialiasing;
        }

        // Pass all the systems that may want to update per-camera data here.
        // That way you will never update an HDCamera and forget to update the dependent system.
        public void Update(FrameSettings currentFrameSettings, VolumetricLightingSystem vlSys, MSAASamples msaaSamples)
        {
            // store a shortcut on HDAdditionalCameraData (done here and not in the constructor as
            // we don't create HDCamera at every frame and user can change the HDAdditionalData later (Like when they create a new scene).
            m_AdditionalCameraData = camera.GetComponent<HDAdditionalCameraData>();

            m_frameSettings = currentFrameSettings;

            UpdateAntialiasingParameters();

            // Handle memory allocation.
            {
                bool isColorPyramidHistoryRequired = m_frameSettings.IsEnabled(FrameSettingsField.SSR); // TODO: TAA as well
                bool isVolumetricHistoryRequired   = m_frameSettings.IsEnabled(FrameSettingsField.Volumetrics) && m_frameSettings.IsEnabled(FrameSettingsField.ReprojectionForVolumetrics);

                int numColorPyramidBuffersRequired = isColorPyramidHistoryRequired ? 2 : 1; // TODO: 1 -> 0
                int numVolumetricBuffersRequired   = isVolumetricHistoryRequired   ? 2 : 0; // History + feedback

                if ((numColorPyramidBuffersAllocated != numColorPyramidBuffersRequired) ||
                    (numVolumetricBuffersAllocated   != numVolumetricBuffersRequired))
                {
                    // Reinit the system.
                    colorPyramidHistoryIsValid = false;
                    vlSys.DeinitializePerCameraData(this);

                    // The history system only supports the "nuke all" option.
                    m_HistoryRTSystem.Dispose();
                    m_HistoryRTSystem = new BufferedRTHandleSystem();

                    if (numColorPyramidBuffersRequired != 0)
                    {
                        AllocHistoryFrameRT((int)HDCameraFrameHistoryType.ColorBufferMipChain, HistoryBufferAllocatorFunction, numColorPyramidBuffersRequired);
                        colorPyramidHistoryIsValid = false;
                    }

                    vlSys.InitializePerCameraData(this, numVolumetricBuffersRequired);

                    // Mark as init.
                    numColorPyramidBuffersAllocated = numColorPyramidBuffersRequired;
                    numVolumetricBuffersAllocated   = numVolumetricBuffersRequired;
                }
            }

            UpdateViewConstants();

            // Update viewport sizes.
            m_ViewportSizePrevFrame = new Vector2Int(m_ActualWidth, m_ActualHeight);
            m_ActualWidth = Math.Max(camera.pixelWidth, 1);
            m_ActualHeight = Math.Max(camera.pixelHeight, 1);

            Vector2Int nonScaledSize = new Vector2Int(m_ActualWidth, m_ActualHeight);
            if (isMainGameView)
            {
                Vector2Int scaledSize = HDDynamicResolutionHandler.instance.GetRTHandleScale(new Vector2Int(camera.pixelWidth, camera.pixelHeight));
                nonScaledSize = HDDynamicResolutionHandler.instance.cachedOriginalSize;
                m_ActualWidth = scaledSize.x;
                m_ActualHeight = scaledSize.y;
            }

            var screenWidth = m_ActualWidth;
            var screenHeight = m_ActualHeight;

            m_LastFrameActive = Time.frameCount;

            // TODO: cache this, or make the history system spill the beans...
            Vector2Int prevColorPyramidBufferSize = Vector2Int.zero;

            if (numColorPyramidBuffersAllocated > 0)
            {
                var rt = GetCurrentFrameRT((int)HDCameraFrameHistoryType.ColorBufferMipChain).rt;

                prevColorPyramidBufferSize.x = rt.width;
                prevColorPyramidBufferSize.y = rt.height;
            }

            // TODO: cache this, or make the history system spill the beans...
            Vector3Int prevVolumetricBufferSize = Vector3Int.zero;

            if (numVolumetricBuffersAllocated != 0)
            {
                var rt = GetCurrentFrameRT((int)HDCameraFrameHistoryType.VolumetricLighting).rt;

                prevVolumetricBufferSize.x = rt.width;
                prevVolumetricBufferSize.y = rt.height;
                prevVolumetricBufferSize.z = rt.volumeDepth;
            }

            m_msaaSamples = msaaSamples;
            // Here we use the non scaled resolution for the RTHandleSystem ref size because we assume that at some point we will need full resolution anyway.
            // This is also useful because we have some RT after final up-rez that will need the full size.
            RTHandles.SetReferenceSize(nonScaledSize.x, nonScaledSize.y, m_msaaSamples);
            m_HistoryRTSystem.SetReferenceSize(nonScaledSize.x, nonScaledSize.y, m_msaaSamples);
            m_HistoryRTSystem.Swap();

            Vector3Int currColorPyramidBufferSize = Vector3Int.zero;

            if (numColorPyramidBuffersAllocated != 0)
            {
                var rt = GetCurrentFrameRT((int)HDCameraFrameHistoryType.ColorBufferMipChain).rt;

                currColorPyramidBufferSize.x = rt.width;
                currColorPyramidBufferSize.y = rt.height;

                if ((currColorPyramidBufferSize.x != prevColorPyramidBufferSize.x) ||
                    (currColorPyramidBufferSize.y != prevColorPyramidBufferSize.y))
                {
                    // A reallocation has happened, so the new texture likely contains garbage.
                    colorPyramidHistoryIsValid = false;
                }
            }

            Vector3Int currVolumetricBufferSize = Vector3Int.zero;

            if (numVolumetricBuffersAllocated != 0)
            {
                var rt = GetCurrentFrameRT((int)HDCameraFrameHistoryType.VolumetricLighting).rt;

                currVolumetricBufferSize.x = rt.width;
                currVolumetricBufferSize.y = rt.height;
                currVolumetricBufferSize.z = rt.volumeDepth;

                if ((currVolumetricBufferSize.x != prevVolumetricBufferSize.x) ||
                    (currVolumetricBufferSize.y != prevVolumetricBufferSize.y) ||
                    (currVolumetricBufferSize.z != prevVolumetricBufferSize.z))
                {
                    // A reallocation has happened, so the new texture likely contains garbage.
                    volumetricHistoryIsValid = false;
                }
            }

            int maxWidth  = RTHandles.maxWidth;
            int maxHeight = RTHandles.maxHeight;

            Vector2 rcpTextureSize = Vector2.one / new Vector2(maxWidth, maxHeight);

            m_ViewportScalePreviousFrame = m_ViewportSizePrevFrame * rcpTextureSize;
            m_ViewportScaleCurrentFrame  = new Vector2Int(m_ActualWidth, m_ActualHeight) * rcpTextureSize;

            screenSize = new Vector4(screenWidth, screenHeight, 1.0f / screenWidth, 1.0f / screenHeight);
            screenParams = new Vector4(screenSize.x, screenSize.y, 1 + screenSize.z, 1 + screenSize.w);

            finalViewport = new Rect(camera.pixelRect.x, camera.pixelRect.y, nonScaledSize.x, nonScaledSize.y);

            if (vlSys != null)
            {
                vlSys.UpdatePerCameraData(this);
            }

            UpdateVolumeParameters();

            m_RecorderCaptureActions = CameraCaptureBridge.GetCaptureActions(camera);
        }

        void UpdateAntialiasingParameters()
        {
            // Handle post-process AA
            //  - If post-processing is disabled all together, no AA
            //  - In scene view, only enable TAA if animated materials are enabled
            //  - Else just use the currently set AA mode on the camera
            {
                if (!m_frameSettings.IsEnabled(FrameSettingsField.Postprocess) || !CoreUtils.ArePostProcessesEnabled(camera))
                    antialiasing = AntialiasingMode.None;
#if UNITY_EDITOR
                else if (camera.cameraType == CameraType.SceneView)
                {
                    var mode = HDRenderPipelinePreferences.sceneViewAntialiasing;

                    if (mode == AntialiasingMode.TemporalAntialiasing && !CoreUtils.AreAnimatedMaterialsEnabled(camera))
                        antialiasing = AntialiasingMode.None;
                    else
                        antialiasing = mode;
                }
#endif
                else if (m_AdditionalCameraData != null)
                {
                    antialiasing = m_AdditionalCameraData.antialiasing;
                    if (antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing)
                    {
                        SMAAQuality = m_AdditionalCameraData.SMAAQuality;
                    }
                }
                else
                    antialiasing = AntialiasingMode.None;
            }

            if (antialiasing != AntialiasingMode.TemporalAntialiasing)
            {
                taaFrameIndex = 0;
                taaJitter = Vector4.zero;
            }

            {
                float t = taaFrameIndex * 0.5f * Mathf.PI;
                taaFrameRotation = new Vector2(Mathf.Sin(t), Mathf.Cos(t));
            }
        }

        void UpdateViewConstants()
        {
            // Allocate required memory to hold view constants
            if (multiviewMatrixView == null || multiviewMatrixView.Length != multiviewCount)
            {
                // renamwe to mv?
                multiviewMatrixView                         = new Matrix4x4[multiviewCount];
                multiviewMatrixViewProj                     = new Matrix4x4[multiviewCount];
                multiviewMatrixViewProjNonJittered          = new Matrix4x4[multiviewCount];
                multiviewMatrixViewProjPrev                 = new Matrix4x4[multiviewCount];
                multiviewMatrixViewProjPrevNoCameraTrans    = new Matrix4x4[multiviewCount];
                multiviewMatrixProj                         = new Matrix4x4[multiviewCount];
                multiviewMatrixProjNonJittered              = new Matrix4x4[multiviewCount];
                multiviewMatrixInvProj                      = new Matrix4x4[multiviewCount];
                multiviewMatrixInvView                      = new Matrix4x4[multiviewCount];
                multiviewMatrixInvViewProj                  = new Matrix4x4[multiviewCount];

                multiviewCameraPos      = new Vector4[multiviewCount];;
                multiviewCameraPosPrev  = new Vector4[multiviewCount];;
            }

            for (int multiviewIndex = 0; multiviewIndex < multiviewCount; ++multiviewIndex)
            {
                var nonJitteredCameraProj = camera.projectionMatrix;
                var viewMatrix = camera.worldToCameraMatrix;
                var cameraPos = camera.transform.position;

                // Override camera data for XR
                if (multipassEnabled || multiviewEnabled)
                {
                    var legacyEyeIndex = multipassEnabled ? legacyMultipassEyeIndex : (Camera.StereoscopicEye)multiviewIndex;

                    nonJitteredCameraProj = camera.GetStereoProjectionMatrix(legacyEyeIndex); // TODO use nonJittered function?
                    viewMatrix = camera.GetStereoViewMatrix(legacyEyeIndex);
                    cameraPos = camera.GetStereoViewMatrix(legacyEyeIndex).inverse.GetColumn(3);
                }

                // If TAA is enabled cameraProj will hold a jittered projection matrix. The original,
                // non-jittered projection matrix can be accessed via nonJitteredProjMatrix.
                var cameraProj = IsTAAEnabled() ? GetJitteredProjectionMatrix(nonJitteredCameraProj) : nonJitteredCameraProj;

                // The actual projection matrix used in shaders is actually massaged a bit to work across all platforms
                // (different Z value ranges etc.)
                var gpuProj = GL.GetGPUProjectionMatrix(cameraProj, true); // Had to change this from 'false'
                var gpuView = viewMatrix;
                var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(nonJitteredCameraProj, true);

                // Zero out the translation component for camera relative code
                if (ShaderConfig.s_CameraRelativeRendering != 0)
                {
                    gpuView.SetColumn(3, new Vector4(0, 0, 0, 1));
                }

                var gpuVP = gpuNonJitteredProj * gpuView;

                // A camera could be rendered multiple times per frame, only updates the previous view proj & pos if needed
                // Note: if your first rendered view during the frame is not the Game view, everything breaks.
                if (m_LastFrameActive != Time.frameCount)
                {
                    if (isFirstFrame)
                    {
                        multiviewCameraPosPrev[multiviewIndex] = cameraPos;
                        multiviewMatrixViewProjPrev[multiviewIndex] = gpuVP;
                    }
                    else
                    {
                        multiviewCameraPosPrev[multiviewIndex] = multiviewCameraPos[multiviewIndex];
                        multiviewMatrixViewProjPrev[multiviewIndex] = nonJitteredCameraProj * viewMatrix;
                        multiviewMatrixViewProjPrevNoCameraTrans[multiviewIndex] = multiviewMatrixViewProjPrev[multiviewIndex];
                    }

                    isFirstFrame = false;
                }

                // In stereo, this corresponds to the center eye position
                // XRTODO
                multiviewCameraPos[multiviewIndex] = cameraPos;

                multiviewMatrixView[multiviewIndex] = gpuView;
                multiviewMatrixProj[multiviewIndex] = gpuProj;
                multiviewMatrixViewProj[multiviewIndex] = gpuProj * gpuView;
                multiviewMatrixViewProjNonJittered[multiviewIndex] = gpuNonJitteredProj;

                //ConfigureStereoMatrices();

                if (ShaderConfig.s_CameraRelativeRendering != 0)
                {
                    multiviewCameraPosPrev[multiviewIndex] = multiviewCameraPos[multiviewIndex] - multiviewCameraPosPrev[multiviewIndex];

                    // This fixes issue with cameraDisplacement stacking in prevViewProjMatrix when same camera renders multiple times each logical frame
                    // causing glitchy motion blur when editor paused.
                    if (m_LastFrameActive != Time.frameCount)
                    {
                        Matrix4x4 cameraDisplacement = Matrix4x4.Translate(multiviewCameraPosPrev[multiviewIndex]);
                        multiviewMatrixViewProjPrev[multiviewIndex] *= cameraDisplacement; // Now prevViewProjMatrix correctly transforms this frame's camera-relative positionWS
                    }
                }
                else
                {
                    Matrix4x4 noTransViewMatrix = viewMatrix;
                    noTransViewMatrix.SetColumn(3, new Vector4(0, 0, 0, 1));
                    multiviewMatrixViewProjPrevNoCameraTrans[multiviewIndex] =  gpuNonJitteredProj * noTransViewMatrix;
                }

                multiviewMatrixInvProj[multiviewIndex]      = multiviewMatrixProj[multiviewIndex].inverse;
                multiviewMatrixInvView[multiviewIndex]      = multiviewMatrixView[multiviewIndex].inverse;
                multiviewMatrixInvViewProj[multiviewIndex]  = multiviewMatrixViewProj[multiviewIndex].inverse;
            }

            // XRTODO:
            var projMatrix = multiviewMatrixProj[0];
            

            float n = camera.nearClipPlane;
            float f = camera.farClipPlane;

            // Analyze the projection matrix.
            // p[2][3] = (reverseZ ? 1 : -1) * (depth_0_1 ? 1 : 2) * (f * n) / (f - n)
            float scale = projMatrix[2, 3] / (f * n) * (f - n);
            bool depth_0_1 = Mathf.Abs(scale) < 1.5f;
            bool reverseZ = scale > 0;
            bool flipProj = projMatrix.inverse.MultiplyPoint(new Vector3(0, 1, 0)).y < 0;

            // http://www.humus.name/temp/Linearize%20depth.txt
            if (reverseZ)
            {
                zBufferParams = new Vector4(-1 + f / n, 1, -1 / f + 1 / n, 1 / f);
            }
            else
            {
                zBufferParams = new Vector4(1 - f / n, f / n, 1 / f - 1 / n, 1 / n);
            }

            projectionParams = new Vector4(flipProj ? -1 : 1, n, f, 1.0f / f);

            float orthoHeight = camera.orthographic ? 2 * camera.orthographicSize : 0;
            float orthoWidth = orthoHeight * camera.aspect;
            unity_OrthoParams = new Vector4(orthoWidth, orthoHeight, 0, camera.orthographic ? 1 : 0);

            // XRTODO (also done in UpdateStereoDependentState)
            Frustum.Create(frustum, multiviewMatrixViewProj[0], depth_0_1, reverseZ);

            // Left, right, top, bottom, near, far.
            for (int i = 0; i < 6; i++)
            {
                frustumPlaneEquations[i] = new Vector4(frustum.planes[i].normal.x, frustum.planes[i].normal.y, frustum.planes[i].normal.z, frustum.planes[i].distance);
            }
        }

        void UpdateVolumeParameters()
        {
            volumeAnchor = null;
            volumeLayerMask = -1;
            if (m_AdditionalCameraData != null)
            {
                volumeLayerMask = m_AdditionalCameraData.volumeLayerMask;
                volumeAnchor = m_AdditionalCameraData.volumeAnchorOverride;
            }
            else
            {
                // Temporary hack:
                // For scene view, by default, we use the "main" camera volume layer mask if it exists
                // Otherwise we just remove the lighting override layers in the current sky to avoid conflicts
                // This is arbitrary and should be editable in the scene view somehow.
                if (camera.cameraType == CameraType.SceneView)
                {
                    var mainCamera = Camera.main;
                    bool needFallback = true;
                    if (mainCamera != null)
                    {
                        var mainCamAdditionalData = mainCamera.GetComponent<HDAdditionalCameraData>();
                        if (mainCamAdditionalData != null)
                        {
                            volumeLayerMask = mainCamAdditionalData.volumeLayerMask;
                            volumeAnchor = mainCamAdditionalData.volumeAnchorOverride;
                            needFallback = false;
                        }
                    }

                    if (needFallback)
                    {
                        HDRenderPipeline hdPipeline = RenderPipelineManager.currentPipeline as HDRenderPipeline;
                        // If the override layer is "Everything", we fall-back to "Everything" for the current layer mask to avoid issues by having no current layer
                        // In practice we should never have "Everything" as an override mask as it does not make sense (a warning is issued in the UI)
                        if (hdPipeline.asset.currentPlatformRenderPipelineSettings.lightLoopSettings.skyLightingOverrideLayerMask == -1)
                            volumeLayerMask = -1;
                        else
                            volumeLayerMask = (-1 & ~hdPipeline.asset.currentPlatformRenderPipelineSettings.lightLoopSettings.skyLightingOverrideLayerMask);
                    }
                }
            }

            // If no override is provided, use the camera transform.
            if (volumeAnchor == null)
                volumeAnchor = camera.transform;
        }

        // Stopgap method used to extract stereo combined matrix state.
        public void UpdateStereoDependentState(ref ScriptableCullingParameters cullingParams)
        {
            if (multiviewEnabled)
            {
                var combinedViewProjMatrix = cullingParams.stereoProjectionMatrix * cullingParams.stereoViewMatrix;
                Frustum.Create(frustum, combinedViewProjMatrix, true, true);

                // Left, right, top, bottom, near, far.
                for (int i = 0; i < 6; i++)
                {
                    frustumPlaneEquations[i] = new Vector4(frustum.planes[i].normal.x, frustum.planes[i].normal.y, frustum.planes[i].normal.z, frustum.planes[i].distance);
                }
            }
        }

        Matrix4x4 GetJitteredProjectionMatrix(Matrix4x4 origProj)
        {
            // The variance between 0 and the actual halton sequence values reveals noticeable
            // instability in Unity's shadow maps, so we avoid index 0.
            float jitterX = HaltonSequence.Get((taaFrameIndex & 1023) + 1, 2) - 0.5f;
            float jitterY = HaltonSequence.Get((taaFrameIndex & 1023) + 1, 3) - 0.5f;
            taaJitter = new Vector4(jitterX, jitterY, jitterX / camera.pixelWidth, jitterY / camera.pixelHeight);

            const int kMaxSampleCount = 8;
            if (++taaFrameIndex >= kMaxSampleCount)
                taaFrameIndex = 0;

            Matrix4x4 proj;

            if (camera.orthographic)
            {
                float vertical = camera.orthographicSize;
                float horizontal = vertical * camera.aspect;

                var offset = taaJitter;
                offset.x *= horizontal / (0.5f * camera.pixelWidth);
                offset.y *= vertical / (0.5f * camera.pixelHeight);

                float left = offset.x - horizontal;
                float right = offset.x + horizontal;
                float top = offset.y + vertical;
                float bottom = offset.y - vertical;

                proj = Matrix4x4.Ortho(left, right, bottom, top, camera.nearClipPlane, camera.farClipPlane);
            }
            else
            {
                var planes = origProj.decomposeProjection;

                float vertFov = Math.Abs(planes.top) + Math.Abs(planes.bottom);
                float horizFov = Math.Abs(planes.left) + Math.Abs(planes.right);

                var planeJitter = new Vector2(jitterX * horizFov / camera.pixelWidth,
                    jitterY * vertFov / camera.pixelHeight);

                planes.left += planeJitter.x;
                planes.right += planeJitter.x;
                planes.top += planeJitter.y;
                planes.bottom += planeJitter.y;

                proj = Matrix4x4.Frustum(planes);
            }

            return proj;
        }

        // Warning: different views can use the same camera!
        public long GetViewID()
        {
            long viewID = camera.GetInstanceID();
            // Make it positive.
            viewID += (-(long)int.MinValue) + 1;
            return viewID;
        }

        public void Reset()
        {
            m_LastFrameActive = -1;
            isFirstFrame = true;
        }

        // Will return NULL if the camera does not exist.
        public static HDCamera Get(Camera camera, int multipassIndex)
        {
            HDCamera hdCamera;

            if (!s_Cameras.TryGetValue(new Tuple<Camera, int>(camera, multipassIndex), out hdCamera))
            {
                hdCamera = null;
            }

            return hdCamera;
        }

        // BufferedRTHandleSystem API expects an allocator function. We define it here.
        static RTHandleSystem.RTHandle HistoryBufferAllocatorFunction(string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            frameIndex &= 1;
            var hdPipeline = (HDRenderPipeline)RenderPipelineManager.currentPipeline;
            
            return rtHandleSystem.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: (GraphicsFormat)hdPipeline.currentPlatformRenderPipelineSettings.colorBufferFormat,
                                        enableRandomWrite: true, useMipMap: true, autoGenerateMips: false, xrInstancing: true,
                                        name: string.Format("CameraColorBufferMipChain{0}", frameIndex));
        }

        // Pass all the systems that may want to initialize per-camera data here.
        // That way you will never create an HDCamera and forget to initialize the data.
        public static HDCamera Create(Camera camera, int multipassIndex)
        {
            HDCamera hdCamera = new HDCamera(camera, multipassIndex);
            s_Cameras.Add(new Tuple<Camera, int>(camera, multipassIndex), hdCamera);

            return hdCamera;
        }

        public static void ClearAll()
        {
            foreach (var cam in s_Cameras)
                cam.Value.ReleaseHistoryBuffer();

            s_Cameras.Clear();
            s_Cleanup.Clear();
        }

        // Look for any camera that hasn't been used in the last frame and remove them from the pool.
        public static void CleanUnused()
        {
            int frameCheck = Time.frameCount - 1;

            foreach (var kvp in s_Cameras)
            {
                if (kvp.Value.m_LastFrameActive < frameCheck)
                    s_Cleanup.Add(kvp.Key);
            }

            foreach (var cam in s_Cleanup)
            {
                var hdCam = s_Cameras[cam];
                if (hdCam.m_HistoryRTSystem != null)
                {
                    hdCam.m_HistoryRTSystem.Dispose();
                    hdCam.m_HistoryRTSystem = null;
                }
                s_Cameras.Remove(cam);
            }

            s_Cleanup.Clear();
        }

        public void SetMultiviewMatrix(CommandBuffer cmd, int nameID, int nameStereoID, Matrix4x4[] values)
        {
            if (multiviewEnabled)
            {
                cmd.SetGlobalMatrixArray(nameStereoID, values);
            }
            else
            {
                cmd.SetGlobalMatrix(nameID, values[0]);
            }
        }

        public void SetMultiviewVector(CommandBuffer cmd, int nameID, int nameStereoID, Vector4[] values)
        {
            if (multiviewEnabled)
            {
                cmd.SetGlobalVectorArray(nameStereoID, values);
            }
            else
            {
                cmd.SetGlobalVector(nameID, values[0]);
            }
        }

        // Set up UnityPerView CBuffer.
        public void SetupGlobalParams(CommandBuffer cmd, float time, float lastTime, uint frameCount)
        {
            bool taaEnabled = m_frameSettings.IsEnabled(FrameSettingsField.Postprocess)
                && antialiasing == AntialiasingMode.TemporalAntialiasing
                && camera.cameraType == CameraType.Game;

            // TODO: order should be the same as CB ?
            SetMultiviewMatrix(cmd, HDShaderIDs._ViewMatrix,                HDShaderIDs._ViewMatrixStereo,                  multiviewMatrixView);
            SetMultiviewMatrix(cmd, HDShaderIDs._InvViewMatrix,             HDShaderIDs._InvViewMatrixStereo,               multiviewMatrixInvView);
            SetMultiviewMatrix(cmd, HDShaderIDs._ProjMatrix,                HDShaderIDs._ProjMatrixStereo,                  multiviewMatrixProj);
            SetMultiviewMatrix(cmd, HDShaderIDs._InvProjMatrix,             HDShaderIDs._InvProjMatrixStereo,               multiviewMatrixInvProj);
            SetMultiviewMatrix(cmd, HDShaderIDs._ViewProjMatrix,            HDShaderIDs._ViewProjMatrixStereo,              multiviewMatrixViewProj);
            SetMultiviewMatrix(cmd, HDShaderIDs._InvViewProjMatrix,         HDShaderIDs._InvViewProjMatrixStereo,           multiviewMatrixInvViewProj);
            SetMultiviewMatrix(cmd, HDShaderIDs._NonJitteredViewProjMatrix, HDShaderIDs._NonJitteredViewProjMatrixStereo,   multiviewMatrixViewProjNonJittered);
            SetMultiviewMatrix(cmd, HDShaderIDs._PrevViewProjMatrix,        HDShaderIDs._PrevViewProjMatrixStereo,          multiviewMatrixViewProjPrev);
            SetMultiviewVector(cmd, HDShaderIDs._WorldSpaceCameraPos,       HDShaderIDs._WorldSpaceCameraPosStereo,         multiviewCameraPos);
            SetMultiviewVector(cmd, HDShaderIDs._PrevCamPosRWS,             HDShaderIDs._PrevCamPosRWSStereo,               multiviewCameraPosPrev);

            //cmd.SetGlobalVectorArray(HDShaderIDs._WorldSpaceCameraPosStereoEyeOffset, worldSpaceCameraPosStereoEyeOffset);

            cmd.SetGlobalVector(HDShaderIDs._ScreenSize,                screenSize);
            cmd.SetGlobalVector(HDShaderIDs._ScreenToTargetScale,       doubleBufferedViewportScale);
            cmd.SetGlobalVector(HDShaderIDs._ZBufferParams,             zBufferParams);
            cmd.SetGlobalVector(HDShaderIDs._ProjectionParams,          projectionParams);
            cmd.SetGlobalVector(HDShaderIDs.unity_OrthoParams,          unity_OrthoParams);
            cmd.SetGlobalVector(HDShaderIDs._ScreenParams,              screenParams);
            cmd.SetGlobalVector(HDShaderIDs._TaaFrameInfo,              new Vector4(taaFrameRotation.x, taaFrameRotation.y, taaFrameIndex, taaEnabled ? 1 : 0));
            cmd.SetGlobalVector(HDShaderIDs._TaaJitterStrength,         taaJitter);
            cmd.SetGlobalVectorArray(HDShaderIDs._FrustumPlanes,        frustumPlaneEquations);

            // Time is also a part of the UnityPerView CBuffer.
            // Different views can have different values of the "Animated Materials" setting.
            bool animateMaterials = CoreUtils.AreAnimatedMaterialsEnabled(camera);

            float  ct = animateMaterials ? time     : 0;
            float  pt = animateMaterials ? lastTime : 0;
            float  dt = Time.deltaTime;
            float sdt = Time.smoothDeltaTime;

            cmd.SetGlobalVector(HDShaderIDs._Time,           new Vector4(ct * 0.05f, ct, ct * 2.0f, ct * 3.0f));
            cmd.SetGlobalVector(HDShaderIDs._LastTime,       new Vector4(pt * 0.05f, pt, pt * 2.0f, pt * 3.0f));
            cmd.SetGlobalVector(HDShaderIDs.unity_DeltaTime, new Vector4(dt, 1.0f / dt, sdt, 1.0f / sdt));
            cmd.SetGlobalVector(HDShaderIDs._SinTime,        new Vector4(Mathf.Sin(ct * 0.125f), Mathf.Sin(ct * 0.25f), Mathf.Sin(ct * 0.5f), Mathf.Sin(ct)));
            cmd.SetGlobalVector(HDShaderIDs._CosTime,        new Vector4(Mathf.Cos(ct * 0.125f), Mathf.Cos(ct * 0.25f), Mathf.Cos(ct * 0.5f), Mathf.Cos(ct)));
            cmd.SetGlobalInt(HDShaderIDs._FrameCount,        (int)frameCount);
        }

        public RTHandleSystem.RTHandle GetPreviousFrameRT(int id)
        {
            return m_HistoryRTSystem.GetFrameRT(id, 1);
        }

        public RTHandleSystem.RTHandle GetCurrentFrameRT(int id)
        {
            return m_HistoryRTSystem.GetFrameRT(id, 0);
        }

        // Allocate buffers frames and return current frame
        public RTHandleSystem.RTHandle AllocHistoryFrameRT(int id, Func<string, int, RTHandleSystem, RTHandleSystem.RTHandle> allocator, int bufferCount)
        {
            m_HistoryRTSystem.AllocBuffer(id, (rts, i) => allocator(camera.name, i, rts), bufferCount);
            return m_HistoryRTSystem.GetFrameRT(id, 0);
        }

        void ReleaseHistoryBuffer()
        {
            m_HistoryRTSystem.ReleaseAll();
        }

        public void ExecuteCaptureActions(RTHandleSystem.RTHandle input, CommandBuffer cmd)
        {
            if (m_RecorderCaptureActions == null || !m_RecorderCaptureActions.MoveNext())
                return;

            // We need to blit to an intermediate texture because input resolution can be bigger than the camera resolution
            // Since recorder does not know about this, we need to send a texture of the right size.
            cmd.GetTemporaryRT(m_RecorderTempRT, actualWidth, actualHeight, 0, FilterMode.Point, input.rt.graphicsFormat);

            var blitMaterial = HDUtils.GetBlitMaterial(TextureDimension.Tex2D);

            m_RecorderPropertyBlock.SetTexture(HDShaderIDs._BlitTexture, input);
            m_RecorderPropertyBlock.SetVector(HDShaderIDs._BlitScaleBias, viewportScale);
            m_RecorderPropertyBlock.SetFloat(HDShaderIDs._BlitMipLevel, 0);
            cmd.SetRenderTarget(m_RecorderTempRT);
            cmd.DrawProcedural(Matrix4x4.identity, blitMaterial, 0, MeshTopology.Triangles, 3, 1, m_RecorderPropertyBlock);

            for (m_RecorderCaptureActions.Reset(); m_RecorderCaptureActions.MoveNext();)
                m_RecorderCaptureActions.Current(m_RecorderTempRT, cmd);
        }
    }
}
