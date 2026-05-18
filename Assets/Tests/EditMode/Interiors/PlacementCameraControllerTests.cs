using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.UI.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class PlacementCameraControllerTests
    {
        [Test]
        public void ClampOrthographicSize_StaysWithinConfiguredRange()
        {
            Assert.AreEqual(4f, HousePlacementCameraController.ClampOrthographicSize(2f, 4f, 12f));
            Assert.AreEqual(12f, HousePlacementCameraController.ClampOrthographicSize(16f, 4f, 12f));
            Assert.AreEqual(8f, HousePlacementCameraController.ClampOrthographicSize(8f, 4f, 12f));
        }

        [Test]
        public void ClampPosition_StaysInsideBoundsForViewport()
        {
            var bounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(20f, 12f, 1f));
            var clamped = HousePlacementCameraController.ClampPosition(new Vector3(20f, 20f, -10f), bounds, 4f, 16f / 9f);

            Assert.LessOrEqual(clamped.x, bounds.max.x);
            Assert.LessOrEqual(clamped.y, bounds.max.y);
            Assert.AreEqual(-10f, clamped.z);
        }

        [Test]
        public void FullViewSize_FramesBoundsWithPadding()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(20f, 10f, 1f));
            var size = HousePlacementCameraController.CalculateFullViewOrthographicSize(bounds, 16f / 9f, 1.08f);

            Assert.GreaterOrEqual(size, 10f * 0.5f * 1.08f);
            Assert.GreaterOrEqual(size, (20f / (16f / 9f)) * 0.5f * 1.08f);
        }

        [Test]
        public void BeginAndEndPlacementControl_RestoresCameraFollowEnabledState()
        {
            var go = new GameObject("PlacementCameraControllerTestCamera", typeof(Camera), typeof(CameraFollow), typeof(HousePlacementCameraController));
            try
            {
                var camera = go.GetComponent<Camera>();
                var follow = go.GetComponent<CameraFollow>();
                var controller = go.GetComponent<HousePlacementCameraController>();

                follow.enabled = true;
                controller.Configure(camera, new Bounds(Vector3.zero, new Vector3(10f, 10f, 1f)));
                controller.BeginPlacementControl();
                Assert.IsFalse(follow.enabled);

                controller.EndPlacementControl();
                Assert.IsTrue(follow.enabled);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
