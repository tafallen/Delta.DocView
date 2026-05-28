namespace Delta.DocView.Client.Services;

public sealed class FilterState
{
    private readonly HashSet<string> _types = new() { "Given", "When", "Then", "And" };
    private readonly HashSet<string> _paramTypes = new();

    public IReadOnlySet<string> Types => _types;
    public string? Domain { get; private set; }
    public IReadOnlySet<string> ParamTypes => _paramTypes;
    public bool FavsOnly { get; private set; }
    public string Query { get; private set; } = "";

    public event Action? Changed;

    public void ToggleType(string type)
    {
        if (_types.Contains(type))
        {
            if (_types.Count == 1) return;
            _types.Remove(type);
        }
        else
        {
            _types.Add(type);
        }
        NotifyChanged();
    }

    public void SetDomain(string? domain)
    {
        if (Domain == domain) return;
        Domain = domain;
        NotifyChanged();
    }

    public void ToggleParamType(string paramType)
    {
        if (!_paramTypes.Remove(paramType))
        {
            _paramTypes.Add(paramType);
        }
        NotifyChanged();
    }

    public void SetFavsOnly(bool value)
    {
        if (FavsOnly == value) return;
        FavsOnly = value;
        NotifyChanged();
    }

    public void SetQuery(string? query)
    {
        var normalised = query ?? "";
        if (Query == normalised) return;
        Query = normalised;
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
