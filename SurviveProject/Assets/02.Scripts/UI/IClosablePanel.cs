namespace Survive.UI
{
    /// <summary>
    /// ESC로 닫을 수 있는 패널.
    /// 패널마다 제각기 입력을 듣게 두면 어떤 것은 닫히고 어떤 것은 안 닫히는
    /// 일이 생긴다. UIStateService 한 곳에서만 ESC를 듣고 여기로 내려보낸다.
    /// </summary>
    public interface IClosablePanel
    {
        bool IsOpen { get; }
        void Close();
    }
}
