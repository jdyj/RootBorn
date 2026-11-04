using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.World;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredSceneUiEligibilityTests
    {
        private Scene _scene;
        [SetUp] public void SetUp() { _scene=EditorSceneManager.NewPreviewScene(); }
        [TearDown] public void TearDown() { if(_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        [Test] public void LIFE_AuthoredScene_QuestChain_InstallsOnce() => AssertConfiguredScene(typeof(QuestChainRuntimeInstaller));
        [Test] public void LIFE_UnconfiguredScene_QuestChain_DoesNotInstall() => AssertUnconfiguredScene(typeof(QuestChainRuntimeInstaller));
        [Test] public void LIFE_AuthoredScene_Campaign_InstallsOnce() => AssertConfiguredScene(typeof(CampaignRuntimeInstaller));
        [Test] public void LIFE_UnconfiguredScene_Campaign_DoesNotInstall() => AssertUnconfiguredScene(typeof(CampaignRuntimeInstaller));
        [Test] public void LIFE_AuthoredScene_CareerCandidate_InstallsOnce() => AssertConfiguredScene(typeof(CareerCandidateRuntimeInstaller));
        [Test] public void LIFE_UnconfiguredScene_CareerCandidate_DoesNotInstall() => AssertUnconfiguredScene(typeof(CareerCandidateRuntimeInstaller));
        [Test] public void LIFE_AuthoredScene_CareerInterest_InstallsOnce() => AssertConfiguredScene(typeof(CareerInterestRuntimeInstaller));
        [Test] public void LIFE_UnconfiguredScene_CareerInterest_DoesNotInstall() => AssertUnconfiguredScene(typeof(CareerInterestRuntimeInstaller));
        [Test] public void LIFE_AuthoredScene_DailyEvent_InstallsOnce() => AssertConfiguredScene(typeof(DailyEventRuntimeInstaller));
        [Test] public void LIFE_UnconfiguredScene_DailyEvent_DoesNotInstall() => AssertUnconfiguredScene(typeof(DailyEventRuntimeInstaller));
        [Test] public void LIFE_AuthoredScene_LocationState_InstallsOnce() => AssertConfiguredScene(typeof(LocationStateRuntimeInstaller));
        [Test] public void LIFE_UnconfiguredScene_LocationState_DoesNotInstall() => AssertUnconfiguredScene(typeof(LocationStateRuntimeInstaller));
        private void AssertConfiguredScene(Type installer)
        {
            var go=new GameObject("AuthoredWorld"); SceneManager.MoveGameObjectToScene(go,_scene); go.AddComponent<AuthoredWorldScene>();
            var method=installer.GetMethod("HandleSceneLoaded",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null);
            method.Invoke(null,new object[]{_scene,LoadSceneMode.Additive}); method.Invoke(null,new object[]{_scene,LoadSceneMode.Additive});
            string runner=(string)installer.GetField("RunnerName",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue();
            Assert.That(_scene.GetRootGameObjects().Count(r=>r.name==runner),Is.EqualTo(1));
            Assert.That(_scene.GetRootGameObjects().Length,Is.EqualTo(2));
        }
        private void AssertUnconfiguredScene(Type installer)
        {
            installer.GetMethod("HandleSceneLoaded",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{_scene,LoadSceneMode.Additive});
            Assert.That(_scene.GetRootGameObjects(),Is.Empty);
        }
    }
}
