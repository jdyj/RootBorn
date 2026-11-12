using System.IO;
using NUnit.Framework;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 가구를 놓는 맵 표시(interactions.json 의 placementSurface): 실제 데이터. 자동 테스트.
    /// 방 칸 자체는 화면(Screens FurnitureRoom)과 서버(Server HouseGrids)가 만들고, 둘이 같은지는 Server SharedHouseRoomTests 가 본다.
    /// </summary>
    public sealed class PlacementSurfaceDataTests
    {
        [Test]
        public void HouseMap_IsMarkedAsTheHouseRoom_InTheInteractionData()
        {
            InteractionData data = InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));
            Assert.That(data.Map("House").PlacementSurface, Is.EqualTo("house"));
            Assert.That(data.Map("School").PlacementSurface, Is.Empty);
            Assert.That(data.Map("Town").PlacementSurface, Is.Empty);
        }
    }
}
