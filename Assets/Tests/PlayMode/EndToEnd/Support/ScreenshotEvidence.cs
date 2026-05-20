using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Rootborn.Tests.PlayMode.EndToEnd.Support
{
    public static class ScreenshotEvidence
    {
        public static void Capture(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(path);
        }

        public static IEnumerator WaitForNonZeroFile(string path, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure + " Path: " + path);
        }

        public static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}