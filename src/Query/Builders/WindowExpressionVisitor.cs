using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query.Builders;
/// <summary>
/// WINDOW句専用ExpressionVisitor（リファクタリング版）
/// </summary>
internal class WindowExpressionVisitor : ExpressionVisitor
{
    private string _windowType = "";
    private string _size = "";
    private string _advanceBy = "";
    private string _gap = "";
    private string _retention = "";
    private string _gracePeriod = "";
    private string _emitBehavior = "";

    /// <summary>
    /// WindowDef訪問
    /// </summary>
    public void VisitWindowDef(WindowDef def)
    {
        foreach (var (Name, Value) in def.Operations)
        {
            ProcessWindowOperation(Name, Value);
        }
    }

    /// <summary>
    /// ウィンドウ操作処理
    /// </summary>
    private void ProcessWindowOperation(string name, object? value)
    {
        switch (name)
        {
            case nameof(WindowDef.TumblingWindow):
                _windowType = "TUMBLING";
                break;

            case nameof(WindowDef.HoppingWindow):
                _windowType = "HOPPING";
                break;

            case nameof(WindowDef.SessionWindow):
                _windowType = "SESSION";
                break;

            case nameof(WindowDef.Size):
                _size = FormatTimeSpan((TimeSpan)value!);
                break;

            case nameof(WindowDef.AdvanceBy):
                _advanceBy = FormatTimeSpan((TimeSpan)value!);
                break;

            case nameof(WindowDef.Gap):
                _gap = FormatTimeSpan((TimeSpan)value!);
                break;

            case nameof(WindowDef.Retention):
                _retention = FormatTimeSpan((TimeSpan)value!);
                break;

            case nameof(WindowDef.GracePeriod):
                _gracePeriod = FormatTimeSpan((TimeSpan)value!);
                break;

            case nameof(WindowDef.EmitFinal):
                _emitBehavior = "FINAL";
                break;
        }
    }

    /// <summary>
    /// ウィンドウ句構築
    /// </summary>
    public string BuildWindowClause()
    {
        return _windowType switch
        {
            "TUMBLING" => BuildTumblingClause(),
            "HOPPING" => BuildHoppingClause(),
            "SESSION" => BuildSessionClause(),
            _ => "UNKNOWN_WINDOW"
        };
    }

    /// <summary>
    /// TUMBLINGウィンドウ句構築
    /// </summary>
    private string BuildTumblingClause()
    {
        var clause = $"TUMBLING (SIZE {_size}";

        // オプション追加
        if (!string.IsNullOrEmpty(_retention))
            clause += $", RETENTION {_retention}";

        if (!string.IsNullOrEmpty(_gracePeriod))
            clause += $", GRACE PERIOD {_gracePeriod}";

        clause += ")";

        // EMIT句追加
        if (!string.IsNullOrEmpty(_emitBehavior))
            clause += $" EMIT {_emitBehavior}";

        return clause;
    }
    /// <summary>
    /// TimeSpanフォーマット
    /// </summary>
    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        // 最適な単位で表現
        if (timeSpan.TotalDays >= 1 && timeSpan.TotalDays == Math.Floor(timeSpan.TotalDays))
            return $"{(int)timeSpan.TotalDays} DAYS";
        if (timeSpan.TotalHours >= 1 && timeSpan.TotalHours == Math.Floor(timeSpan.TotalHours))
            return $"{(int)timeSpan.TotalHours} HOURS";
        if (timeSpan.TotalMinutes >= 1 && timeSpan.TotalMinutes == Math.Floor(timeSpan.TotalMinutes))
            return $"{(int)timeSpan.TotalMinutes} MINUTES";
        if (timeSpan.TotalSeconds >= 1 && timeSpan.TotalSeconds == Math.Floor(timeSpan.TotalSeconds))
            return $"{(int)timeSpan.TotalSeconds} SECONDS";
        if (timeSpan.TotalMilliseconds >= 1)
            return $"{(int)timeSpan.TotalMilliseconds} MILLISECONDS";

        return "0 SECONDS";
    }
    /// <summary>
    /// HOPPINGウィンドウ句構築
    /// </summary>
    private string BuildHoppingClause()
    {
        var clause = $"HOPPING (SIZE {_size}";

        // ADVANCE BY は必須ではないが、指定されていれば追加
        if (!string.IsNullOrEmpty(_advanceBy))
            clause += $", ADVANCE BY {_advanceBy}";

        // オプション追加
        if (!string.IsNullOrEmpty(_retention))
            clause += $", RETENTION {_retention}";

        if (!string.IsNullOrEmpty(_gracePeriod))
            clause += $", GRACE PERIOD {_gracePeriod}";

        clause += ")";

        // EMIT句追加
        if (!string.IsNullOrEmpty(_emitBehavior))
            clause += $" EMIT {_emitBehavior}";

        return clause;
    }

    /// <summary>
    /// SESSIONウィンドウ句構築
    /// </summary>
    private string BuildSessionClause()
    {
        if (string.IsNullOrEmpty(_gap))
        {
            throw new InvalidOperationException("SESSION window requires GAP specification");
        }

        var clause = $"SESSION (GAP {_gap})";

        // セッションウィンドウはRETENTION、GRACE PERIOD、EMIT FINALをサポートしない
        // 警告表示
        if (!string.IsNullOrEmpty(_retention) || !string.IsNullOrEmpty(_gracePeriod) || !string.IsNullOrEmpty(_emitBehavior))
        {
            Console.WriteLine("[KSQL-LINQ WARNING] SESSION windows do not support RETENTION, GRACE PERIOD, or EMIT FINAL options");
        }

        return clause;
    }

    // TimeSpanフォーマット等のメソッドは省略（既に実装済み）
}
