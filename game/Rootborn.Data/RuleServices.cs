using System;
using Rootborn.Core.Gathering;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data
{
    /// <summary>
    /// 정의를 만들 때 규칙 객체에 넘기는 실행 서비스. 데이터만으로는 할 수 없는 일(다른 세이브 파일 읽기·쓰기)을 하는 결과가 받는다.
    /// Host는 저장소를 만든 뒤 GameDataLoader.Load(dir, b => RootbornBuilders.Register(b, new RuleServices(store)))로 읽는다.
    /// 서비스 없이 읽으면(RootbornBuilders.Register(b)) 그런 결과는 아무것도 하지 않는다.
    /// </summary>
    public sealed class RuleServices
    {
        public RuleServices(ISaveStore saveStore)
        {
            SaveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
            RecipeReveal = new ResourceRecipeReveal(saveStore);
        }

        public ISaveStore SaveStore { get; }

        /// <summary>장소 활동의 레시피 공개(resource-recipe-progress*.json).</summary>
        public ILocationActivityRecipeReveal RecipeReveal { get; }
    }
}
