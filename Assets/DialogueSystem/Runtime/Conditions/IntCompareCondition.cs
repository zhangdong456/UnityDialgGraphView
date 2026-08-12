using System;

namespace DialogueSystem
{
    public enum CompareOperator
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual
    }

    /// <summary>
    /// 整数比较条件:比较黑板上的一个整数值。
    /// 例如"金币数大于等于 50":key = "gold", op = GreaterOrEqual, value = 50。
    /// </summary>
    [DialogueEditorName("整数比较", "比较黑板中的整数值")]
    [Serializable]
    public class IntCompareCondition : DialogueCondition
    {
        public string key = "gold";
        public CompareOperator op = CompareOperator.GreaterOrEqual;
        public int value;

        public override bool Evaluate(DialogueContext context)
        {
            int v = context.Blackboard.GetInt(key);
            switch (op)
            {
                case CompareOperator.Equal: return v == value;
                case CompareOperator.NotEqual: return v != value;
                case CompareOperator.Less: return v < value;
                case CompareOperator.LessOrEqual: return v <= value;
                case CompareOperator.Greater: return v > value;
                case CompareOperator.GreaterOrEqual: return v >= value;
                default: return false;
            }
        }

        public override string GetSummary()
        {
            string symbol;
            switch (op)
            {
                case CompareOperator.Equal: symbol = "=="; break;
                case CompareOperator.NotEqual: symbol = "!="; break;
                case CompareOperator.Less: symbol = "<"; break;
                case CompareOperator.LessOrEqual: symbol = "<="; break;
                case CompareOperator.Greater: symbol = ">"; break;
                default: symbol = ">="; break;
            }
            return $"{key} {symbol} {value}";
        }
    }
}
