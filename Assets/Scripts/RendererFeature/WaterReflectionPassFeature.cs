/*
 * Original Code: URP-WaterReflectionTest
 * Author: rngtm
 * Source: https://github.com/rngtm/URP-WaterReflectionTest
 * Article: https://zenn.dev/r_ngtm/articles/urp-water-reflection
 *
 * Modified for Unity 6 RenderGraph by: yuki4080
 * Repository: https://github.com/yuki4080/URP-WaterReflectionTest
 * Description: Ported to Unity 6 RenderGraph API.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

public class WaterReflectionPassFeature : ScriptableRendererFeature
{
    #region Fields
    [SerializeField] public Settings settings = new Settings();
    
    private RenderReflectionObjectPass _renderObjectPass;
    private MergeReflectionPass _mergeReflectionPass;
    private RTHandle _reflectionHandle;
    #endregion

    // 設定
    [Serializable]
    public class Settings
    {
        // 水面の高さ (Y座標)
        public float waterY = 0f;

        // Skyboxをレンダリングする
        public bool renderSkybox = false;
        
        // レンダリング対象のレイヤーマスク
        public LayerMask cullingMask = -1;

        // レンダリングタイプ
        public RenderQueueType renderQueueType = RenderQueueType.Opaque;

        // 反射をレンダリングするタイミング
        public RenderPassEvent renderObjectPassEvent = RenderPassEvent.BeforeRenderingOpaques;

        // レンダリング結果をフレームバッファへ合成するタイミング (デバッグ用)
        public RenderPassEvent debugPassEvent = RenderPassEvent.AfterRenderingTransparents;

        // trueにすると、反射のデバッグ表示
        public bool debugReflection = false;
    }

    #region Defines
    // RenderTexture名の定義
    public static class RenderTextureNames
    {
        public static string _CameraReflectionTexture = "_CameraReflectionTexture";
        public static string _CameraReflectionDepthTexture = "_CameraReflectionDepthTexture";
    }

    // シェーダープロパティIDの定義
    public static class ShaderPropertyIDs
    {
        public static readonly int _CameraReflectionTexture = Shader.PropertyToID(RenderTextureNames._CameraReflectionTexture);
        public static readonly int _CameraReflectionDepthTexture = Shader.PropertyToID(RenderTextureNames._CameraReflectionDepthTexture);
    }
    #endregion
    
    #region RenderPass

    /// <summary>
    /// 反射オブジェクトを描画するパス
    /// </summary>
    class RenderReflectionObjectPass : ScriptableRenderPass
    {
        private readonly string k_ProfilerTag = nameof(RenderReflectionObjectPass);
        private WaterPlane _waterPlane;
        private RTHandle _reflectionHandle;
        
        // レンダリング対象のShaderTag
        private List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId> 
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
        };

        public Settings Settings { get; set; }

        public void Setup(RTHandle reflectionHandle)
        {
            _reflectionHandle = reflectionHandle;
        }

        class PassData
        {
            public RendererListHandle RendererList;
            public RendererListHandle SkyboxRendererList;
            public Matrix4x4 ViewMatrix;
            public Matrix4x4 ProjectionMatrix;
            public Matrix4x4 DefaultViewMatrix;
            public bool RenderSkybox;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_reflectionHandle == null || _reflectionHandle.rt == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();

            if (_waterPlane == null) _waterPlane = WaterPlane.Instance;
            if (_waterPlane != null) Settings.waterY = _waterPlane.WaterY;

            // RenderTexture確保
            TextureHandle reflectionColor = renderGraph.ImportTexture(_reflectionHandle);

            // デプスバッファ用の一時テクスチャ作成
            var targetDesc = cameraData.cameraTargetDescriptor;
            var depthDesc = new TextureDesc(targetDesc.width, targetDesc.height);
            depthDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
            depthDesc.depthBufferBits = DepthBits.Depth32;
            depthDesc.msaaSamples = (MSAASamples)targetDesc.msaaSamples;
            depthDesc.name = "_CameraReflectionDepth";
            
            TextureHandle reflectionDepth = renderGraph.CreateTexture(depthDesc);

            var viewMatrix = cameraData.GetViewMatrix();
            var defaultViewMatrix = viewMatrix;
            
            // Y座標をwaterYだけ平行移動する行列
            var translateMat = Matrix4x4.identity;
            translateMat.m13 = -Settings.waterY;
            
            // Y軸反転する行列
            var reverseMat = Matrix4x4.identity;
            reverseMat.m11 = -reverseMat.m11;
            
            // 水面反転を行うように、View行列を加工する
            // 変換後の頂点座標 = P * V * Reverse * Translate * M * 頂点座標
            viewMatrix = viewMatrix * reverseMat * translateMat;

            var projectionMatrix = cameraData.GetProjectionMatrix();
            projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, true);

            // RendererList作成
            // レンダリング対象とするRenderQueue
            var renderQueueRange = (Settings.renderQueueType == RenderQueueType.Transparent)
                ? RenderQueueRange.transparent
                : RenderQueueRange.opaque;
            
            // フィルタリング設定
            var filterSettings = new FilteringSettings(renderQueueRange, Settings.cullingMask);

            // オブジェクトのソート設定
            var sortFlags = (Settings.renderQueueType == RenderQueueType.Transparent)
                ? SortingCriteria.CommonTransparent
                : cameraData.defaultOpaqueSortFlags;

            // 描画設定
            var drawSettings = new DrawingSettings(m_ShaderTagIdList[0], new SortingSettings(cameraData.camera) { criteria = sortFlags });
            for (int i = 1; i < m_ShaderTagIdList.Count; i++) drawSettings.SetShaderPassName(i, m_ShaderTagIdList[i]);
            
            var rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
            RendererListHandle rendererList = renderGraph.CreateRendererList(rendererListParams);

            RendererListHandle skyboxList = new RendererListHandle();
            if (Settings.renderSkybox) skyboxList = renderGraph.CreateSkyboxRendererList(cameraData.camera);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_ProfilerTag, out var passData))
            {
                passData.RendererList = rendererList;
                passData.SkyboxRendererList = skyboxList;
                passData.ViewMatrix = viewMatrix;
                passData.ProjectionMatrix = projectionMatrix;
                passData.DefaultViewMatrix = defaultViewMatrix;
                passData.RenderSkybox = Settings.renderSkybox;

                // レンダリング先の変更
                builder.SetRenderAttachment(reflectionColor, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(reflectionDepth, AccessFlags.Write);
                
                builder.UseRendererList(rendererList);
                if (Settings.renderSkybox) builder.UseRendererList(skyboxList);

                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(reflectionColor, ShaderPropertyIDs._CameraReflectionTexture);
                builder.SetGlobalTextureAfterPass(reflectionDepth, ShaderPropertyIDs._CameraReflectionDepthTexture);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, data.ViewMatrix, data.ProjectionMatrix, false);
                    
                    // カリング反転 (ビュー行列を反転すると、メッシュの表・裏が逆転するため)
                    cmd.SetInvertCulling(true);
                    
                    // 描画クリア
                    cmd.ClearRenderTarget(true, true, Color.black);

                    // レンダリング実行
                    if (data.RenderSkybox) cmd.DrawRendererList(data.SkyboxRendererList);
                    cmd.DrawRendererList(data.RendererList);

                    // 元に戻す
                    cmd.SetInvertCulling(false);
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, data.DefaultViewMatrix, data.ProjectionMatrix, false);
                });
            }
        }
    }

    /// <summary>
    /// 反射をフレームバッファへ合成するパス (デバッグ用)
    /// </summary>
    class MergeReflectionPass : ScriptableRenderPass
    {
        private readonly string k_ProfilerTag = nameof(MergeReflectionPass);
        private RTHandle _reflectionHandle;

        public void Setup(RTHandle reflectionHandle)
        {
            _reflectionHandle = reflectionHandle;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_reflectionHandle == null || _reflectionHandle.rt == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle activeColor = resourceData.activeColorTexture;
            TextureHandle sourceTexture = renderGraph.ImportTexture(_reflectionHandle);

            renderGraph.AddBlitPass(sourceTexture, activeColor, Vector2.one, Vector2.zero, passName: k_ProfilerTag);
        }
    }
    #endregion

    public override void Create()
    {
        // Render Pass 作成
        _renderObjectPass = new RenderReflectionObjectPass();
        _renderObjectPass.Settings = settings;
        _renderObjectPass.renderPassEvent = settings.renderObjectPassEvent;

        _mergeReflectionPass = new MergeReflectionPass();
        _mergeReflectionPass.renderPassEvent = settings.debugPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        var desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0; 
        
        // RTHandle 確保
        RenderingUtils.ReAllocateHandleIfNeeded(ref _reflectionHandle, desc, name: RenderTextureNames._CameraReflectionTexture);

        _renderObjectPass.Setup(_reflectionHandle);
        renderer.EnqueuePass(_renderObjectPass);

        if (settings.debugReflection)
        {
            _mergeReflectionPass.Setup(_reflectionHandle);
            renderer.EnqueuePass(_mergeReflectionPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        // 確保したRTHandleを解放
        _reflectionHandle?.Release();
        _reflectionHandle = null;
    }
}
