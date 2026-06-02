using System.Text.Json.Serialization;

namespace Delta.DocView.Shared.Models;

public sealed class StepParam
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("example")]
    public string Example { get; init; } = "";

    /// <summary>
    /// How the table column definitions were derived, when this param accepts a SpecFlow
    /// DataTable. Null for ordinary (non-table) params.
    /// </summary>
    [JsonPropertyName("columnsSource")]
    public string? ColumnsSource { get; init; }

    /// <summary>
    /// Column definitions for a DataTable-accepting param.
    /// Empty for ordinary (non-table) params.
    /// </summary>
    [JsonPropertyName("columns")]
    public IReadOnlyList<TableColumn> Columns { get; init; } = [];

    /// <summary>True when this param represents a SpecFlow DataTable argument.</summary>
    [JsonIgnore]
    public bool HasTable => Columns.Count > 0 || ColumnsSource is not null;
}

/// <summary>A column definition within a DataTable param.</summary>
public sealed class TableColumn
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";
}
