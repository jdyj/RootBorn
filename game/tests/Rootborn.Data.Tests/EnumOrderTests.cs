using System;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// data/의 열거형은 Unity가 쓴 숫자다. 만들기 함수가 읽는 열거형마다 Core(또는 Data의 대응표) 순서가
    /// 원본 Assets 소스의 선언 순서와 같은지 고정한다. 기대 배열은 원본 소스를 그대로 옮겨 적은 것이다.
    /// </summary>
    public sealed class EnumOrderTests
    {
        [Test]
        public void LifeActivityCategory_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/StudentLifeCore.cs
            Assert.That(Enum.GetNames(typeof(LifeActivityCategory)),
                Is.EqualTo(new[] { "School", "Work", "Hobby", "Errand", "Social", "Rest", "SelfStudy" }));
        }

        [Test]
        public void ItemCategory_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/Common/GameDataRegistry.cs
            Assert.That(Enum.GetNames(typeof(ItemCategory)), Is.EqualTo(new[] { "Resource", "Tool", "Misc", "Seed" }));
        }

        [Test]
        public void CareerCandidateState_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/CareerCandidateCore.cs
            Assert.That(Enum.GetNames(typeof(CareerCandidateState)),
                Is.EqualTo(new[] { "Locked", "Hinted", "Revealed", "Deepening", "ReadyForChoice" }));
        }

        [Test]
        public void MinigameBonusTarget_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/Minigames/GrowthMinigameBonusRule.cs
            Assert.That(Enum.GetNames(typeof(MinigameBonusTarget)),
                Is.EqualTo(new[] { "TimingWindow", "TimeLimit", "MistakeAllowance", "HintCount", "Harmony", "Steadiness" }));
        }

        [Test]
        public void DiscoveryScope_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/DiscoveryDefinition.cs
            Assert.That(Enum.GetNames(typeof(DiscoveryScope)), Is.EqualTo(new[] { "Personal", "SharedWorld" }));
        }

        [Test]
        public void LocationGrowthRoute_NumbersMapToTheCoreNames_InTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/LocationActivityDefinition.cs enum LocationGrowthRoute.
            // Core는 문자열(LocationGrowthRoutes)로 받으므로 Data의 대응표를 고정한다.
            Assert.That(LocationBuilders.GrowthRouteNames, Is.EqualTo(new[]
            {
                LocationGrowthRoutes.FormalStudy, LocationGrowthRoutes.SelfStudy, LocationGrowthRoutes.Work,
                LocationGrowthRoutes.Commerce, LocationGrowthRoutes.SocialHelp, LocationGrowthRoutes.Rest,
                LocationGrowthRoutes.Exploration,
            }));
            Assert.That(LocationBuilders.GrowthRouteNames,
                Is.EqualTo(new[] { "FormalStudy", "SelfStudy", "Work", "Commerce", "SocialHelp", "Rest", "Exploration" }));
        }

        [Test]
        public void LocationActivityProgressTarget_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/LocationActivityProgressDeltaOutcome.cs
            Assert.That(Enum.GetNames(typeof(LocationBuilders.ProgressTarget)),
                Is.EqualTo(new[] { "Trait", "Skill", "Relationship", "Status", "CareerHint" }));
        }
    }
}
