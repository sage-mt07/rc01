using System;

namespace Kafka.Ksql.Linq.Core.Validation;
/// <summary>
/// 削除済み: CoreValidationResult (重複定義)
/// メイン定義: Core/Abstractions/ValidationResult.cs
/// 削除理由: 同一機能の重複、Core/Abstractions配下に統一
/// </summary>
[Obsolete("CoreValidationResult は削除されました。Core.Abstractions.ValidationResult を使用してください。", true)]
public class CoreValidationResult
{
    [Obsolete("CoreValidationResult は削除されました。Core.Abstractions.ValidationResult を使用してください。", true)]
    public bool IsValid { get; set; }

    [Obsolete("CoreValidationResult は削除されました。Core.Abstractions.ValidationResult を使用してください。", true)]
    public System.Collections.Generic.List<string> Errors { get; set; } = new();

    [Obsolete("CoreValidationResult は削除されました。Core.Abstractions.ValidationResult を使用してください。", true)]
    public System.Collections.Generic.List<string> Warnings { get; set; } = new();
}