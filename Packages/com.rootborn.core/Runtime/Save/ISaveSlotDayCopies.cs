namespace Rootborn.Core.Save
{
    /// <summary>
    /// 날마다 슬롯 전체의 사본을 남길 수 있는 저장소(멀티플레이 설계 6절, 혼자 하기에도 쓴다). 사본이 어디에 어떤 모양으로 놓이는지는 구현이 정한다.
    /// 되살리기는 저장소가 하지 않는다(사본 파일을 슬롯에 복사해 넣으면 그 날로 읽힌다).
    /// </summary>
    public interface ISaveSlotDayCopies
    {
        /// <summary>
        /// 지금 슬롯의 파일 전부를 dayNumber 날의 사본으로 남긴다(같은 날 사본이 있으면 바꾼다). 그 뒤 날 번호가 큰 것부터 keepCount개만 남기고 지운다.
        /// keepCount가 0 이하이면 아무것도 하지 않는다.
        /// </summary>
        void KeepDayCopy(string saveSlot, int dayNumber, int keepCount);
    }
}
