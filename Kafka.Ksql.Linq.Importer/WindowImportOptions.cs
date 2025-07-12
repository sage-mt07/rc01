namespace Kafka.Ksql.Linq.Window.Importer;
    public class WindowImportOptions
    {
        public int BatchSize { get; set; } = 1000;
        public int BatchDelayMs { get; set; } = 100;
        public bool EnableDetailedLogging { get; set; } = false;
    }


public class DatabaseImportConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;
    public string TableName { get; set; } = string.Empty;
    public string KeyColumn { get; set; } = string.Empty;
    public string TimestampColumn { get; set; } = string.Empty;
    public int WindowMinutes { get; set; } = 5;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<AggregationColumn> AggregationColumns { get; set; } = new();
    public string EntityType { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300;
}

public class CsvImportConfig
{
    public string FilePath { get; set; } = string.Empty;
    public char Delimiter { get; set; } = ',';
    public string KeyColumn { get; set; } = string.Empty;
    public string TimestampColumn { get; set; } = string.Empty;
    public int WindowMinutes { get; set; } = 5;
    public Dictionary<string, string> ValueColumns { get; set; } = new(); // CSV列名 -> 集約データキー
    public string EntityType { get; set; } = string.Empty;
    public bool FailOnError { get; set; } = false;
}

public class JsonImportConfig
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsDirectWindowFormat { get; set; } = true;
    public string EntityType { get; set; } = string.Empty;
    public string KeyPath { get; set; } = "$.key";
    public string TimestampPath { get; set; } = "$.timestamp";
    public string DataPath { get; set; } = "$.data";
}

public class DirectoryImportConfig
{
    public string DirectoryPath { get; set; } = string.Empty;
    public string FilePattern { get; set; } = "*.*";
    public string EntityType { get; set; } = string.Empty;
    public bool FailOnError { get; set; } = false;
    public CsvImportConfig DefaultCsvConfig { get; set; } = new();
    public JsonImportConfig DefaultJsonConfig { get; set; } = new();

    public CsvImportConfig ToCsvConfig(string filePath)
    {
        var config = new CsvImportConfig
        {
            FilePath = filePath,
            Delimiter = DefaultCsvConfig.Delimiter,
            KeyColumn = DefaultCsvConfig.KeyColumn,
            TimestampColumn = DefaultCsvConfig.TimestampColumn,
            WindowMinutes = DefaultCsvConfig.WindowMinutes,
            ValueColumns = DefaultCsvConfig.ValueColumns,
            EntityType = EntityType,
            FailOnError = FailOnError
        };
        return config;
    }

    public JsonImportConfig ToJsonConfig(string filePath)
    {
        var config = new JsonImportConfig
        {
            FilePath = filePath,
            IsDirectWindowFormat = DefaultJsonConfig.IsDirectWindowFormat,
            EntityType = EntityType,
            KeyPath = DefaultJsonConfig.KeyPath,
            TimestampPath = DefaultJsonConfig.TimestampPath,
            DataPath = DefaultJsonConfig.DataPath
        };
        return config;
    }
}

public class AggregationColumn
{
    public string ColumnName { get; set; } = string.Empty;
    public string AggregateFunction { get; set; } = "SUM"; // SUM, AVG, MAX, MIN, COUNT
    public string Alias { get; set; } = string.Empty;
}

public enum DatabaseType
{
    SqlServer,
    PostgreSQL,
    MySQL
}
