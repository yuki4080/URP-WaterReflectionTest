using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class ReflectionRendererFeatureTests
{
    private const string TestScenePath = "Assets/Tests/Scenes/TestScene.unity";
    private const string ReferenceImagePath = "Assets/Tests/ReferenceImages";

    private ScriptableRendererData _rendererData;
    private WaterReflectionPassFeature _targetFeature;
    private ImageComparisonSettings _commonSettings;

    private bool _originalRenderSkybox;
    private bool _originalDebugReflection;

    [UnitySetUp]
    public IEnumerator Setup()
    {
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
            TestScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync("TestScene");
#endif

        var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        Assert.IsNotNull(pipelineAsset);

        var type = typeof(UniversalRenderPipelineAsset);
        var fieldInfo = type.GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        var rendererDataList = fieldInfo?.GetValue(pipelineAsset) as ScriptableRendererData[];

        Assert.IsNotNull(rendererDataList);
        _rendererData = rendererDataList[0];

        _targetFeature = _rendererData.rendererFeatures.Find(f => f is WaterReflectionPassFeature) as WaterReflectionPassFeature;
        Assert.IsNotNull(_targetFeature);

        _originalRenderSkybox = _targetFeature.settings.renderSkybox;
        _originalDebugReflection = _targetFeature.settings.debugReflection;

        _commonSettings = new ImageComparisonSettings
        {
            TargetWidth = 1920,
            TargetHeight = 1080,
            UseBackBuffer = true,
            AverageCorrectnessThreshold = 0.0001f,
        };
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (_targetFeature != null)
        {
            _targetFeature.settings.renderSkybox = _originalRenderSkybox;
            _targetFeature.settings.debugReflection = _originalDebugReflection;
            _rendererData.SetDirty();
        }
        yield return null;
    }

    private void CaptureAndCompare(string testName)
    {
#if UNITY_EDITOR
        string fileName = $"{testName}.png";
        string assetPath = Path.Combine(ReferenceImagePath, fileName);

        Texture2D refImage = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

        if (refImage != null)
        {
            MakeTextureReadable(assetPath);
            refImage = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            ImageAssert.AreEqual(refImage, Camera.main, _commonSettings);
        }
        else
        {
            if (!Directory.Exists(ReferenceImagePath))
            {
                Directory.CreateDirectory(ReferenceImagePath);
            }

            var screenTex = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] bytes = screenTex.EncodeToPNG();
            
            File.WriteAllBytes(assetPath, bytes);
            
            AssetDatabase.Refresh();

            MakeTextureReadable(assetPath);

            Assert.Ignore($"First run: Created reference image at {fileName}. Please run test again.");
        }
#else
        Assert.Fail("Reference image not found in player build.");
#endif
    }

    private void MakeTextureReadable(string path)
    {
#if UNITY_EDITOR
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            
            if (!importer.isReadable) 
            { 
                importer.isReadable = true; 
                changed = true; 
            }
            
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
#endif
    }

    [UnityTest]
    public IEnumerator ReflectionTest_Basic()
    {
        ApplySettings(renderSkybox: true, debugReflection: false);
        yield return null;
        yield return new WaitForEndOfFrame();

        CaptureAndCompare("ReflectionTest_Basic");
    }

    [UnityTest]
    public IEnumerator ReflectionTest_WithoutSkybox()
    {
        ApplySettings(renderSkybox: false, debugReflection: false);
        yield return null;
        yield return new WaitForEndOfFrame();

        CaptureAndCompare("ReflectionTest_WithoutSkybox");
    }

    [UnityTest]
    public IEnumerator ReflectionTest_Basic_WithDebug()
    {
        ApplySettings(renderSkybox: true, debugReflection: true);
        yield return null;
        yield return new WaitForEndOfFrame();

        CaptureAndCompare("ReflectionTest_Basic_WithDebug");
    }

    [UnityTest]
    public IEnumerator ReflectionTest_WithoutSkybox_WithDebug()
    {
        ApplySettings(renderSkybox: false, debugReflection: true);
        yield return null;
        yield return new WaitForEndOfFrame();

        CaptureAndCompare("ReflectionTest_WithoutSkybox_WithDebug");
    }

    private void ApplySettings(bool renderSkybox, bool debugReflection)
    {
        _targetFeature.settings.renderSkybox = renderSkybox;
        _targetFeature.settings.debugReflection = debugReflection;
        _rendererData.SetDirty();
    }
}
