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
        //Material m_ObjectMaterial;
        //MotionData m_MotionData;
        
        ShaderTagId m_ShaderTagId = new ShaderTagId("MotionVectors");
        
        internal MotionVectorsPass(RenderPassEvent evt)
        {
            // Set data
            base.profilingSampler = new ProfilingSampler(k_ProfilingTag);
            renderPassEvent = evt;
        }
        
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Configure Render Target
            m_MotionVectorHandle.Init(k_MotionVectorTexture);
            cmd.GetTemporaryRT(m_MotionVectorHandle.id, cameraTextureDescriptor, FilterMode.Point);
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
                //Shader.SetGlobalMatrix(k_PreviousViewProjectionMatrix, m_MotionData.previousViewProjectionMatrix);

                // These flags are still required in SRP or the engine won't compute previous model matrices...
                // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                // Drawing
                //DrawCameraMotionVectors(context, cmd, camera);
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
}