namespace Survive.Core
{
    /// <summary>
    /// 체크포인트 저장 대상. SaveService가 CaptureState의 반환값을 JSON으로 직렬화한다.
    /// </summary>
    public interface ISaveable
    {
        string SaveKey { get; }
        object CaptureState();
        void RestoreState(object state);
    }
}
