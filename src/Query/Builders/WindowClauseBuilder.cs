using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Query.Builders.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query.Builders;

/// <summary>
/// WINDOW句内容構築ビルダー
/// 設計理由：責務分離設計に準拠、キーワード除外で純粋なウィンドウ定義内容のみ生成
/// 出力例: "TUMBLING (SIZE 5 MINUTES)" (WINDOW除外)
/// </summary>
internal class WindowClauseBuilder : BuilderBase
{
    public override KsqlBuilderType BuilderType => KsqlBuilderType.Window;

    protected override KsqlBuilderType[] GetRequiredBuilderTypes()
    {
        return Array.Empty<KsqlBuilderType>(); // 他Builderに依存しない
    }

    protected override string BuildInternal(Expression expression)
    {
        var visitor = new WindowExpressionVisitor();

        // 式の種類に応じた処理
        switch (expression)
        {
            case ConstantExpression { Value: WindowDef def }:
                visitor.VisitWindowDef(def);
                break;

            case ConstantExpression { Value: TimeSpan ts }:
                visitor.VisitWindowDef(TumblingWindow.Of(ts));
                break;

            default:
                visitor.Visit(expression);
                break;
        }

        return visitor.BuildWindowClause();
    }

    protected override void ValidateBuilderSpecific(Expression expression)
    {
        // WINDOW句特有のバリデーション
        ValidateWindowExpression(expression);
    }

    /// <summary>
    /// ウィンドウ式バリデーション
    /// </summary>
    private static void ValidateWindowExpression(Expression expression)
    {
        // WindowDefまたはTimeSpanの確認
        if (expression is ConstantExpression constant)
        {
            if (constant.Value is WindowDef windowDef)
            {
                ValidateWindowDef(windowDef);
            }
            else if (constant.Value is TimeSpan timeSpan)
            {
                ValidateTimeSpan(timeSpan);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Window expression must be WindowDef or TimeSpan, but got {constant.Value?.GetType().Name}");
            }
        }
        else if (expression is MethodCallExpression methodCall)
        {
            ValidateWindowMethodCall(methodCall);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported window expression type: {expression.GetType().Name}");
        }
    }

    /// <summary>
    /// WindowDef バリデーション
    /// </summary>
    private static void ValidateWindowDef(WindowDef windowDef)
    {
        var operations = windowDef.Operations;

        // 基本的なウィンドウタイプの存在確認
        var hasWindowType = operations.Any(op =>
            op.Name == nameof(WindowDef.TumblingWindow) ||
            op.Name == nameof(WindowDef.HoppingWindow) ||
            op.Name == nameof(WindowDef.SessionWindow));

        if (!hasWindowType)
        {
            throw new InvalidOperationException("Window definition must specify window type (Tumbling, Hopping, or Session)");
        }

        // セッションウィンドウの場合はGapが必須
        if (operations.Any(op => op.Name == nameof(WindowDef.SessionWindow)))
        {
            if (!operations.Any(op => op.Name == nameof(WindowDef.Gap)))
            {
                throw new InvalidOperationException("Session window requires Gap specification");
            }
        }

        // HoppingウィンドウはSizeとAdvanceByが必要
        if (operations.Any(op => op.Name == nameof(WindowDef.HoppingWindow)))
        {
            if (!operations.Any(op => op.Name == nameof(WindowDef.Size)))
            {
                throw new InvalidOperationException("Hopping window requires Size specification");
            }
        }

        // 時間値の妥当性チェック
        ValidateWindowTiming(operations);
    }

    /// <summary>
    /// TimeSpan バリデーション
    /// </summary>
    private static void ValidateTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Window time span must be positive");
        }

        if (timeSpan.TotalDays > 30)
        {
            throw new InvalidOperationException(
                "Window time span should not exceed 30 days for performance reasons");
        }

        if (timeSpan.TotalSeconds < 1)
        {
            throw new InvalidOperationException(
                "Window time span should be at least 1 second");
        }
    }

    /// <summary>
    /// ウィンドウメソッド呼び出しバリデーション
    /// </summary>
    private static void ValidateWindowMethodCall(MethodCallExpression methodCall)
    {
        var validMethods = new[]
        {
            "TumblingWindow", "HoppingWindow", "SessionWindow",
            "Size", "AdvanceBy", "Gap", "Retention", "GracePeriod", "EmitFinal"
        };

        if (!validMethods.Contains(methodCall.Method.Name))
        {
            throw new InvalidOperationException(
                $"Method '{methodCall.Method.Name}' is not supported in window expressions");
        }
    }

    /// <summary>
    /// ウィンドウタイミングバリデーション
    /// </summary>
    private static void ValidateWindowTiming(List<(string Name, object? Value)> operations)
    {
        var sizeOp = operations.FirstOrDefault(op => op.Name == nameof(WindowDef.Size));
        var advanceByOp = operations.FirstOrDefault(op => op.Name == nameof(WindowDef.AdvanceBy));
        var gapOp = operations.FirstOrDefault(op => op.Name == nameof(WindowDef.Gap));
        var retentionOp = operations.FirstOrDefault(op => op.Name == nameof(WindowDef.Retention));
        var graceOp = operations.FirstOrDefault(op => op.Name == nameof(WindowDef.GracePeriod));

        // 各TimeSpanパラメータの妥当性チェック
        foreach (var op in new[] { sizeOp, advanceByOp, gapOp, retentionOp, graceOp })
        {
            if (op.Value is TimeSpan ts)
            {
                ValidateTimeSpan(ts);
            }
        }

        // HoppingウィンドウでAdvanceBy > Sizeは非推奨
        if (sizeOp.Value is TimeSpan size && advanceByOp.Value is TimeSpan advanceBy)
        {
            if (advanceBy > size)
            {
                Console.WriteLine("[KSQL-LINQ WARNING] AdvanceBy greater than Size may cause gaps in data coverage");
            }
        }

        // セッションウィンドウのGapが大きすぎる場合の警告
        if (gapOp.Value is TimeSpan gap && gap.TotalHours > 24)
        {
            Console.WriteLine("[KSQL-LINQ WARNING] Session window gap over 24 hours may impact performance");
        }
    }
}
