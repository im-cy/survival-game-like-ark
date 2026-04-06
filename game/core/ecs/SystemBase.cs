namespace SurvivalGame.Core.ECS
{
    /// <summary>
    /// 所有游戏系统的基类。
    /// GameManager 统一调用 Tick，各系统处理对应组件集合的逻辑。
    /// </summary>
    public abstract class SystemBase
    {
        public bool Enabled { get; set; } = true;

        /// <summary>每帧/每 tick 由 GameManager 调用</summary>
        public abstract void Tick(float delta);

        /// <summary>系统初始化（GameManager._Ready 后调用一次）</summary>
        public virtual void Initialize() { }
    }
}
