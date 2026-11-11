namespace Rootborn.Server
{
    /// <summary>데이터 지문은 Rootborn.Data.DataFingerprint 한 곳에서 셈한다(Host도 같은 것을 쓴다). 이 이름은 서버 쪽 부르는 곳을 위해 남긴 통로다.</summary>
    public static class DataFingerprint
    {
        public static string Compute(string dataDirectory) => Rootborn.Data.DataFingerprint.Compute(dataDirectory);
    }
}
