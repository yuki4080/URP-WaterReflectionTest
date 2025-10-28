using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;
using UnityEngine.SceneManagement;

public class ReflectionRendererFeatureTests : MonoBehaviour
{
    private const string TestScenePath = "Assets/Tests/Scenes/TestScene.unity";
    private const string ExpectedImagesPath = "Assets/Tests/ExpectedImages";

    [SetUp]
    public void Setup()
    {
        SceneManager.LoadScene(TestScenePath, LoadSceneMode.Single);
        PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "Full HD");
    }

    [UnityTest]
    public IEnumerator ReflectionTest_Basic()
    {
        yield return new WaitForEndOfFrame();
        var settings = new ImageComparisonSettings()
        {
            TargetWidth = Screen.width,
            TargetHeight = Screen.height,
        };

        var expected = ExpectedImage();
        ImageAssert.AreEqual(expected, Camera.main, settings);
    }
    
        private static Texture2D ExpectedImage()
        {
            Texture2D expected = null;

            var expectedFile = TestContext.CurrentTestExecutionContext.CurrentTest.Name
                .Replace('(', '_')
                .Replace(')', '_')
                .Replace(',', '-');

            var expectedPath = Path.GetFullPath(Path.Combine(
                ExpectedImagesPath,
                $"{expectedFile}.png"));

            if (File.Exists(expectedPath))
            {
                var bytes = File.ReadAllBytes(Path.GetFullPath(expectedPath));
                expected = new Texture2D(Screen.width, Screen.height);
                expected.LoadImage(bytes);
            }

            return expected;
        }

        private static Texture2D CaptureScreenshotAsTexture(TextureFormat format = TextureFormat.ARGB32)
        {
            var src = ScreenCapture.CaptureScreenshotAsTexture();
            var dst = new Texture2D(src.width, src.height, format, false);
            dst.SetPixels(src.GetPixels());
            return dst;
        }

    [TearDown]
    public void TearDown()
    {
    }
}
