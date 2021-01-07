using System;

namespace UnityEngine.Rendering.Universal.Internal
{
    public class MotionVectorsPass : ScriptableRenderPass
    {
        const string k_PreviousViewProjectionMatrix = "_PrevViewProjMatrix";
        const string k_MotionVectorTexture = "_MotionVectorTexture";
        const string k_ProfilingTag = "Motion Vectors";
        
        RenderTargetHandle m_MotionVectorHandle;
        Material m_CameraMaterial;
        MotionData m_MotionData;
        
        ShaderTagId m_ShaderTagId = new ShaderTagId("MotionVectors");
        
        internal MotionVectorsPass(RenderPassEvent evt, Material cameraMotionVectorsMaterial)
        {
            // Set data
            base.profilingSampler = new ProfilingSampler(k_ProfilingTag);
            renderPassEvent = evt;
            m_CameraMaterial = cameraMotionVectorsMaterial;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        
        internal void Setup(MotionData motionData)
        {
            // Set data
            m_MotionData = motionData;
        }
        
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Configure Render Target
            m_MotionVectorHandle.Init(k_MotionVectorTexture);
            var motionVectorsTextureDescriptor = cameraTextureDescriptor;
            motionVectorsTextureDescriptor.colorFormat = RenderTextureFormat.RGHalf;
            
            cmd.GetTemporaryRT(m_MotionVectorHandle.id, motionVectorsTextureDescriptor, FilterMode.Point);
            ConfigureTarget(m_MotionVectorHandle.Identifier(), m_MotionVectorHandle.Identifier());
            cmd.SetRenderTarget(m_MotionVectorHandle.Identifier(), m_MotionVectorHandle.Identifier());
            cmd.ClearRenderTarget(true, true, Color.black, 1.0f);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Get data
            var camera = renderingData.cameraData.camera;

            // Never draw in Preview
            if(camera.cameraType == CameraType.Preview)
                return;

            // Profiling command
            CommandBuffer cmd = CommandBufferPool.Get(k_ProfilingTag);
            using (new ProfilingSample(cmd, k_ProfilingTag))
            {
                ExecuteCommand(context, cmd);

                // Shader uniforms
                Shader.SetGlobalMatrix(k_PreviousViewProjectionMatrix, m_MotionData.previousViewProjectionMatrix);

                // These flags are still required in SRP or the engine won't compute previous model matrices...
                // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                // Drawing
                DrawCameraMotionVectors(context, cmd, camera);
                DrawObjectMotionVectors(context, ref renderingData, cmd, camera);
            }
            ExecuteCommand(context, cmd);
        }
        
        DrawingSettings GetDrawingSettings(ref RenderingData renderingData)
        {
            // Drawing Settings
            var camera = renderingData.cameraData.camera;
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(m_ShaderTagId, sortingSettings)
            {
                perObjectData = PerObjectData.MotionVectors,
                enableDynamicBatching = renderingData.supportsDynamicBatching,
                enableInstancing = true,
            };
            
            return drawingSettings;
        }
        
        void DrawCameraMotionVectors(ScriptableRenderContext context, CommandBuffer cmd, Camera camera)
        {
            // Draw fullscreen quad
            cmd.DrawProcedural(Matrix4x4.identity, m_CameraMaterial, 0, MeshTopology.Triangles, 3, 1);
            ExecuteCommand(context, cmd);
        }

        void DrawObjectMotionVectors(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, Camera camera)
        {
            // Get CullingParameters
            var cullingParameters = new ScriptableCullingParameters();
            if (!camera.TryGetCullingParameters(out cullingParameters))
                return;

            // Culling Results
            var cullingResults = context.Cull(ref cullingParameters);

            var drawingSettings = GetDrawingSettings(ref renderingData);
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, camera.cullingMask);
            var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            
            // Draw Renderers
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
        }
        
        void ExecuteCommand(ScriptableRenderContext context, CommandBuffer cmd)
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
    }
    
    internal sealed class MotionData
    {
        #region Fields
        bool m_IsFirstFrame;
        int m_LastFrameActive;
        Matrix4x4 m_ViewProjectionMatrix;
        Matrix4x4 m_PreviousViewProjectionMatrix;
        #endregion

        #region Constructors
        internal MotionData()
        {
            // Set data
            m_IsFirstFrame = true;
            m_LastFrameActive = -1;
            m_ViewProjectionMatrix = Matrix4x4.identity;
            m_PreviousViewProjectionMatrix = Matrix4x4.identity;
        }
        #endregion

        #region Properties
        internal bool isFirstFrame
        {
            get => m_IsFirstFrame;
            set => m_IsFirstFrame = value;
        }

        internal int lastFrameActive
        {
            get => m_LastFrameActive;
            set => m_LastFrameActive = value;
        }

        internal Matrix4x4 viewProjectionMatrix
        {
            get => m_ViewProjectionMatrix;
            set => m_ViewProjectionMatrix = value;
        }

        internal Matrix4x4 previousViewProjectionMatrix
        {
            get => m_PreviousViewProjectionMatrix;
            set => m_PreviousViewProjectionMatrix = value;
        }
        #endregion
    }
}