namespace Delta.DocView.Client.Services;

/// <summary>
/// Scoped state container for the four filter axes (step type, domain, parameter type, favourites)
/// plus the search query. Raises <see cref="Changed"/> on any real change.
/// </summary>
public sealed class FilterState
{
    private readonly HashSet<string> _types = new(GherkinStepTypes.All);
    private readonly HashSet<string> _paramTypes = new();

    public IReadOnlySet<string> Types => _types;
    public string? Domain { get; private set; }
    public IReadOnlySet<string> ParamTypes => _paramTypes;
    public bool FavsOnly { get; private set; }
    public string Query { get; private set; } = "";

    /// <summary>Raised when any filter axis or the query changes.</summary>
    /// <remarks>
    /// Subscribers MUST unsubscribe on disposal (e.g. components implementing IDisposable
    /// and calling -= in Dispose). Failing to do so leaks the closure for the lifetime of
    /// this scoped service.
    /// </remarks>
    public event Action? Changed;

    /// <summary>Toggles a step type on or off.</summary>
    /// <remarks>
    /// Refuses to remove the last remaining step type; calling <see cref="ToggleType"/> on the only
    /// active type is a no-op (no state change, no event).
    /// </remarks>
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

    /// <summary>Sets the active domain filter, or clears it when <paramref name="domain"/> is null.</summary>
    public void SetDomain(string? domain)
    {
        if (Domain == domain) return;
        Domain = domain;
        NotifyChanged();
    }

    /// <summary>Toggles a parameter type on or off in the active set.</summary>
    public void ToggleParamType(string paramType)
    {
        if (!_paramTypes.Remove(paramType))
        {
            _paramTypes.Add(paramType);
        }
        NotifyChanged();
    }

    /// <summary>Enables or disables the favourites-only filter.</summary>
    public void SetFavsOnly(bool value)
    {
        if (FavsOnly == value) return;
        FavsOnly = value;
        NotifyChanged();
    }

    /// <summary>Sets the search query. A null value is normalised to an empty string.</summary>
    public void SetQuery(string? query)
    {
        var normalised = query ?? "";
        if (Query == normalised) return;
        Query = normalised;
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
