using Kafka.Ksql.Linq.Window.Finalization;
using Kafka.Ksql.Linq.Window.Importer;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.Json;
using System.Data.SqlClient;

namespace Kafka.Ksql.Linq.Window.Import;

public class WindowDataImporter : IDisposable
{
    private readonly ILogger<WindowDataImporter> _logger;
    private readonly IKafkaProducer _finalTopicProducer;
    private readonly WindowImportOptions _options;
    private bool _disposed = false;

    public WindowDataImporter(
        IKafkaProducer finalTopicProducer,
        WindowImportOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        _finalTopicProducer = finalTopicProducer ?? throw new ArgumentNullException(nameof(finalTopicProducer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = loggerFactory?.CreateLogger<WindowDataImporter>()
                 ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowDataImporter>.Instance;
    }

    /// <summary>
    /// 既存DBから足データをインポート
    /// SQL Server/PostgreSQL/MySQL等から直接取得
    /// </summary>
    public async Task ImportFromDatabase(DatabaseImportConfig config)
    {
        _logger.LogInformation("Starting database import: {ConnectionString}",
            MaskConnectionString(config.ConnectionString));

        var importedCount = 0;

        try
        {
            using var connection = CreateDatabaseConnection(config.ConnectionString, config.DatabaseType);
             connection.Open();

            var query = BuildAggregationQuery(config);
            _logger.LogDebug("Executing query: {Query}", query);

            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = config.TimeoutSeconds;

            using var reader =  command.ExecuteReader();
            var windowBatch = new List<WindowFinalMessage>();

            while ( reader.Read())
            {
                var windowMessage = MapDatabaseRowToWindow(reader, config);
                if (windowMessage != null)
                {
                    windowBatch.Add(windowMessage);

                    if (windowBatch.Count >= _options.BatchSize)
                    {
                        await SendWindowBatch(windowBatch, config.EntityType);
                        importedCount += windowBatch.Count;
                        windowBatch.Clear();

                        _logger.LogInformation("Imported {Count} windows from database", importedCount);
                    }
                }
            }

            // 残りのバッチを送信
            if (windowBatch.Count > 0)
            {
                await SendWindowBatch(windowBatch, config.EntityType);
                importedCount += windowBatch.Count;
            }

            _logger.LogInformation("Database import completed: {Count} windows imported", importedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database import failed");
            throw;
        }
    }

    /// <summary>
    /// CSVファイルから足データをインポート
    /// 取引所データ、既存システムエクスポート等に対応
    /// </summary>
    public async Task ImportFromCsv(CsvImportConfig config)
    {
        _logger.LogInformation("Starting CSV import: {FilePath}", config.FilePath);

        if (!File.Exists(config.FilePath))
        {
            throw new FileNotFoundException($"CSV file not found: {config.FilePath}");
        }

        var importedCount = 0;
        var windowBatch = new List<WindowFinalMessage>();

        try
        {
            var lines = await File.ReadAllLinesAsync(config.FilePath);
            if (lines.Length == 0)
            {
                _logger.LogWarning("CSV file is empty: {FilePath}", config.FilePath);
                return;
            }

            var headers = ParseCsvHeaders(lines[0], config);
            ValidateCsvHeaders(headers, config);

            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var values = ParseCsvLine(lines[i], config);
                    var windowMessage = MapCsvRowToWindow(values, headers, config, i);

                    if (windowMessage != null)
                    {
                        windowBatch.Add(windowMessage);

                        if (windowBatch.Count >= _options.BatchSize)
                        {
                            await SendWindowBatch(windowBatch, config.EntityType);
                            importedCount += windowBatch.Count;
                            windowBatch.Clear();

                            if (importedCount % 10000 == 0)
                            {
                                _logger.LogInformation("Imported {Count} windows from CSV", importedCount);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse CSV line {LineNumber}: {Line}",
                        i + 1, lines[i]);

                    if (config.FailOnError)
                    {
                        throw;
                    }
                }
            }

            // 残りのバッチを送信
            if (windowBatch.Count > 0)
            {
                await SendWindowBatch(windowBatch, config.EntityType);
                importedCount += windowBatch.Count;
            }

            _logger.LogInformation("CSV import completed: {Count} windows imported from {TotalLines} lines",
                importedCount, lines.Length - 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV import failed: {FilePath}", config.FilePath);
            throw;
        }
    }

    /// <summary>
    /// JSONファイルから足データをインポート
    /// 既存システムからのエクスポートデータに対応
    /// </summary>
    public async Task ImportFromJson(JsonImportConfig config)
    {
        _logger.LogInformation("Starting JSON import: {FilePath}", config.FilePath);

        if (!File.Exists(config.FilePath))
        {
            throw new FileNotFoundException($"JSON file not found: {config.FilePath}");
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(config.FilePath);

            List<WindowFinalMessage> windowData;

            if (config.IsDirectWindowFormat)
            {
                // 直接WindowFinalMessage形式
                windowData = JsonSerializer.Deserialize<List<WindowFinalMessage>>(jsonContent)
                           ?? new List<WindowFinalMessage>();
            }
            else
            {
                // カスタム形式から変換
                var rawData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                windowData = ConvertJsonToWindows(rawData, config);
            }

            if (windowData.Count == 0)
            {
                _logger.LogWarning("No valid window data found in JSON file: {FilePath}", config.FilePath);
                return;
            }

            // バッチ送信
            await SendWindowsInBatches(windowData, config.EntityType);

            _logger.LogInformation("JSON import completed: {Count} windows imported", windowData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON import failed: {FilePath}", config.FilePath);
            throw;
        }
    }

    /// <summary>
    /// 複数ファイル一括インポート
    /// ディレクトリ内の全ファイルを処理
    /// </summary>
    public async Task ImportFromDirectory(DirectoryImportConfig config)
    {
        _logger.LogInformation("Starting directory import: {Directory}", config.DirectoryPath);

        if (!Directory.Exists(config.DirectoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {config.DirectoryPath}");
        }

        var files = Directory.GetFiles(config.DirectoryPath, config.FilePattern, SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f)
                             .ToArray();

        if (files.Length == 0)
        {
            _logger.LogWarning("No matching files found in directory: {Directory} with pattern: {Pattern}",
                config.DirectoryPath, config.FilePattern);
            return;
        }

        var totalImported = 0;
        var successCount = 0;
        var failureCount = 0;

        foreach (var filePath in files)
        {
            try
            {
                _logger.LogInformation("Processing file {Current}/{Total}: {FileName}",
                    Array.IndexOf(files, filePath) + 1, files.Length, Path.GetFileName(filePath));

                var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
                var beforeCount = totalImported;

                switch (fileExtension)
                {
                    case ".csv":
                        var csvConfig = config.ToCsvConfig(filePath);
                        await ImportFromCsv(csvConfig);
                        break;

                    case ".json":
                        var jsonConfig = config.ToJsonConfig(filePath);
                        await ImportFromJson(jsonConfig);
                        break;

                    default:
                        _logger.LogWarning("Unsupported file format: {FilePath}", filePath);
                        continue;
                }

                successCount++;
                _logger.LogInformation("File processed successfully: {FileName}", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex, "Failed to process file: {FilePath}", filePath);

                if (config.FailOnError)
                {
                    throw;
                }
            }
        }

        _logger.LogInformation("Directory import completed: {Success} success, {Failed} failed, {Total} total files",
            successCount, failureCount, files.Length);
    }

    // ===================================================================
    // プライベートヘルパーメソッド
    // ===================================================================

    private IDbConnection CreateDatabaseConnection(string connectionString, DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
            DatabaseType.PostgreSQL => new Npgsql.NpgsqlConnection(connectionString),
            DatabaseType.MySQL => new MySql.Data.MySqlClient.MySqlConnection(connectionString),
            _ => throw new NotSupportedException($"Database type {dbType} is not supported")
        };
    }

    private string BuildAggregationQuery(DatabaseImportConfig config)
    {
        var windowFunction = config.DatabaseType switch
        {
            DatabaseType.SqlServer => $"DATEPART(HOUR, {config.TimestampColumn}) * 60 + (DATEPART(MINUTE, {config.TimestampColumn}) / {config.WindowMinutes}) * {config.WindowMinutes}",
            DatabaseType.PostgreSQL => $"EXTRACT(HOUR FROM {config.TimestampColumn}) * 60 + (EXTRACT(MINUTE FROM {config.TimestampColumn}) / {config.WindowMinutes}) * {config.WindowMinutes}",
            DatabaseType.MySQL => $"HOUR({config.TimestampColumn}) * 60 + (MINUTE({config.TimestampColumn}) DIV {config.WindowMinutes}) * {config.WindowMinutes}",
            _ => throw new NotSupportedException($"Database type {config.DatabaseType} is not supported")
        };

        return $@"
SELECT 
    {config.KeyColumn} as EntityKey,
    DATE({config.TimestampColumn}) as WindowDate,
    {windowFunction} as WindowMinutes,
    COUNT(*) as EventCount,
    {string.Join(", ", config.AggregationColumns.Select(col => $"{col.AggregateFunction}({col.ColumnName}) as {col.Alias}"))}
FROM {config.TableName}
WHERE {config.TimestampColumn} >= '{config.StartDate:yyyy-MM-dd HH:mm:ss}'
  AND {config.TimestampColumn} <= '{config.EndDate:yyyy-MM-dd HH:mm:ss}'
GROUP BY {config.KeyColumn}, DATE({config.TimestampColumn}), {windowFunction}
ORDER BY {config.KeyColumn}, DATE({config.TimestampColumn}), {windowFunction}";
    }

    private WindowFinalMessage? MapDatabaseRowToWindow(IDataReader reader, DatabaseImportConfig config)
    {
        try
        {
            var entityKey = reader["EntityKey"].ToString();
            var windowDate = (DateTime)reader["WindowDate"];
            var windowMinutes = Convert.ToInt32(reader["WindowMinutes"]);
            var eventCount = Convert.ToInt32(reader["EventCount"]);

            var windowStart = windowDate.AddMinutes(windowMinutes);
            var windowKey = GenerateWindowKey(entityKey!, windowStart, config.WindowMinutes);

            var aggregatedData = new Dictionary<string, object>();
            foreach (var col in config.AggregationColumns)
            {
                aggregatedData[col.Alias] = reader[col.Alias];
            }

            return new WindowFinalMessage
            {
                WindowKey = windowKey,
                WindowStart = windowStart,
                WindowEnd = windowStart.AddMinutes(config.WindowMinutes),
                WindowMinutes = config.WindowMinutes,
                EventCount = eventCount,
                AggregatedData = aggregatedData,
                FinalizedAt = DateTime.UtcNow,
                PodId = "db_importer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map database row to window");
            return null;
        }
    }

    private async Task SendWindowBatch(List<WindowFinalMessage> windows, string entityType)
    {
        var finalTopic = $"{entityType}_window_final";

        foreach (var window in windows)
        {
            await _finalTopicProducer.SendAsync(finalTopic, window.WindowKey, window);
        }

        if (_options.BatchDelayMs > 0)
        {
            await Task.Delay(_options.BatchDelayMs);
        }
    }

    private async Task SendWindowsInBatches(List<WindowFinalMessage> windows, string entityType)
    {
        var totalBatches = (windows.Count + _options.BatchSize - 1) / _options.BatchSize;

        for (int i = 0; i < totalBatches; i++)
        {
            var batch = windows.Skip(i * _options.BatchSize).Take(_options.BatchSize).ToList();
            await SendWindowBatch(batch, entityType);

            _logger.LogInformation("Sent batch {Current}/{Total} ({Count} windows)",
                i + 1, totalBatches, batch.Count);
        }
    }

    private string GenerateWindowKey(string entityKey, DateTime windowStart, int windowMinutes)
    {
        return $"{entityKey}_{windowStart:yyyyMMddHHmm}_{windowMinutes}min";
    }

    private string MaskConnectionString(string connectionString)
    {
        // パスワード部分をマスク
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString,
            @"(password|pwd)=([^;]+)",
            "$1=***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private string[] ParseCsvHeaders(string headerLine, CsvImportConfig config)
    {
        return headerLine.Split(config.Delimiter)
                        .Select(h => h.Trim().Trim('"'))
                        .ToArray();
    }

    private string[] ParseCsvLine(string line, CsvImportConfig config)
    {
        return line.Split(config.Delimiter)
                  .Select(v => v.Trim().Trim('"'))
                  .ToArray();
    }

    private void ValidateCsvHeaders(string[] headers, CsvImportConfig config)
    {
        var requiredColumns = new[] { config.KeyColumn, config.TimestampColumn }
            .Concat(config.ValueColumns.Keys);

        foreach (var required in requiredColumns)
        {
            if (!headers.Contains(required))
            {
                throw new InvalidOperationException($"Required column '{required}' not found in CSV headers");
            }
        }
    }

    private WindowFinalMessage? MapCsvRowToWindow(string[] values, string[] headers, CsvImportConfig config, int lineNumber)
    {
        try
        {
            var dataDict = headers.Zip(values, (h, v) => new { Header = h, Value = v })
                                 .ToDictionary(x => x.Header, x => x.Value);

            var entityKey = dataDict[config.KeyColumn];
            var timestampStr = dataDict[config.TimestampColumn];

            if (!DateTime.TryParse(timestampStr, out var timestamp))
            {
                _logger.LogWarning("Invalid timestamp format at line {Line}: {Timestamp}", lineNumber, timestampStr);
                return null;
            }

            var windowStart = CalculateWindowStart(timestamp, config.WindowMinutes);
            var windowKey = GenerateWindowKey(entityKey, windowStart, config.WindowMinutes);

            var aggregatedData = new Dictionary<string, object>();
            foreach (var col in config.ValueColumns)
            {
                if (dataDict.TryGetValue(col.Key, out var valueStr))
                {
                    aggregatedData[col.Value] = ParseValue(valueStr, col.Value);
                }
            }

            return new WindowFinalMessage
            {
                WindowKey = windowKey,
                WindowStart = windowStart,
                WindowEnd = windowStart.AddMinutes(config.WindowMinutes),
                WindowMinutes = config.WindowMinutes,
                EventCount = 1, // CSVの場合は通常1行1集約結果
                AggregatedData = aggregatedData,
                FinalizedAt = DateTime.UtcNow,
                PodId = "csv_importer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map CSV row to window at line {Line}", lineNumber);
            return null;
        }
    }

    private DateTime CalculateWindowStart(DateTime timestamp, int windowMinutes)
    {
        var totalMinutes = timestamp.Hour * 60 + timestamp.Minute;
        var windowStartMinutes = (totalMinutes / windowMinutes) * windowMinutes;
        var hours = windowStartMinutes / 60;
        var minutes = windowStartMinutes % 60;

        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, hours, minutes, 0);
    }

    private object ParseValue(string valueStr, string targetType)
    {
        return targetType.ToLowerInvariant() switch
        {
            "decimal" or "money" => decimal.TryParse(valueStr, out var d) ? d : 0m,
            "int" or "count" => int.TryParse(valueStr, out var i) ? i : 0,
            "double" or "avg" => double.TryParse(valueStr, out var db) ? db : 0.0,
            _ => valueStr
        };
    }

    private List<WindowFinalMessage> ConvertJsonToWindows(JsonElement jsonData, JsonImportConfig config)
    {
        var windows = new List<WindowFinalMessage>();

        // JSON構造に応じて変換ロジックを実装
        // この部分は実際のJSONフォーマットに合わせてカスタマイズ

        return windows;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogInformation("WindowDataImporter disposed");
        }
    }
}
