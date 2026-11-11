namespace Rootborn.Core.Gathering
{
    /// <summary>
    /// 레시피 하나(원본 Assets/Scripts/Game/Crafting/RecipeDefinition.cs)의 지금 게임이 쓰는 값: 공개된 레시피를 목표 저널에 보일 때의
    /// 이름과 다음 힌트. 만들기 단계(steps)와 만드는 도구(producesTool)는 만들기 기능을 부르는 곳이 없어 옮기지 않았다.
    /// </summary>
    public sealed class RecipeDefinition
    {
        public RecipeDefinition(string id, string displayNameKey, string nextHintKey)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            NextHintKey = nextHintKey ?? string.Empty;
        }

        public string Id { get; }

        /// <summary>비면 id(원본과 같다).</summary>
        public string DisplayNameKey { get; }

        /// <summary>비면 화면이 기본 힌트 문구를 쓴다.</summary>
        public string NextHintKey { get; }
    }
}
