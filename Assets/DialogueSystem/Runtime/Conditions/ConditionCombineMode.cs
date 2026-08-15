namespace DialogueSystem
{
    /// <summary>
    /// 一个选项/分支内多个条件的组合方式。
    /// All = 全部满足(并,AND);Any = 任一满足(或,OR)。
    /// </summary>
    public enum ConditionCombineMode
    {
        /// <summary>全部满足(并)。默认值,与旧版行为一致。</summary>
        All = 0,

        /// <summary>任一满足(或)。忽略列表中的空元素。</summary>
        Any = 1
    }
}
