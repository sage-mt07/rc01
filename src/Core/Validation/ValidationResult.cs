using System;

namespace Kafka.Ksql.Linq.Core.Validation;
/// <summary>
/// 削除済み: CoreValidationResult (重複定義)
/// メイン定義: Core/Abstractions/ValidationResult.cs
/// 削除理由: 同一機能の重複、Core/Abstractions配下に統一
/// </summary>
[Obsolete("CoreValidationResult has been removed. Use Core.Abstractions.ValidationResult instead.", true)]
public class CoreValidationResult
{
    [Obsolete("CoreValidationResult has been removed. Use Core.Abstractions.ValidationResult instead.", true)]
    public bool IsValid { get; set; }

    [Obsolete("CoreValidationResult has been removed. Use Core.Abstractions.ValidationResult instead.", true)]
    public System.Collections.Generic.List<string> Errors { get; set; } = new();

    [Obsolete("CoreValidationResult has been removed. Use Core.Abstractions.ValidationResult instead.", true)]
    public System.Collections.Generic.List<string> Warnings { get; set; } = new();
}
