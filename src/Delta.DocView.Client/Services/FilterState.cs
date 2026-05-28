namespace Delta.DocView.Client.Services;

public sealed class FilterState
{
    public ISet<string> Types { get; } = new HashSet<string> { "Given", "When", "Then", "And" };
    public string? Domain { get; private set; }
    public ISet<string> ParamTypes { get; } = new HashSet<string>();
    public bool FavsOnly { get; private set; }
    public string Query { get; private set; } = "";

    public event Action? OnChanged;

    public void ToggleType(string type)
    {
        if (Types.Contains(type))
        {
            if (Types.Count == 1) return;
            Types.Remove(type);
        }
        else
        {
            Types.Add(type);
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
        if (!ParamTypes.Remove(paramType))
        {
            ParamTypes.Add(paramType);
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

    private void NotifyChanged() => OnChanged?.Invoke();
}
