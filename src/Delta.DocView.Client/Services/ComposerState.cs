using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

public sealed class ComposerState : IDisposable
{
    private readonly IKeyboardActions _actions;
    private readonly List<ComposerItem> _steps = [];
    private bool _isOpen;
    private string _scenarioName = "";
    private string? _featureTextCache;

    public IReadOnlyList<ComposerItem> Steps => _steps;
    public bool IsOpen => _isOpen;
    public string ScenarioName => _scenarioName;
    public int StepCount => _steps.Count;
    public string FeatureText => _featureTextCache ??= FeatureTextBuilder.Build(_scenarioName, _steps);

    /// <summary>
    /// Filename suggested for "Save .feature": the scenario name slugified
    /// (lower-case, non-alphanumeric runs collapsed to '-', trimmed) plus the
    /// .feature extension. Falls back to "scenario.feature" when the name is
    /// blank or slugifies to nothing.
    /// </summary>
    public string SuggestedFileName => Slugify(_scenarioName) + ".feature";

    private static string Slugify(string name)
    {
        var slug = new System.Text.StringBuilder();
        var prevDash = false;
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) { slug.Append(c); prevDash = false; }
            else if (!prevDash && slug.Length > 0) { slug.Append('-'); prevDash = true; }
        }
        var result = slug.ToString().Trim('-');
        return result.Length == 0 ? "scenario" : result;
    }

    public event Action? Changed;

    public ComposerState(IKeyboardActions actions)
    {
        _actions = actions;
        _actions.ToggleComposerRequested += Toggle;
        _actions.CloseOverlayRequested   += Close;
    }

    public void Toggle()
    {
        _isOpen = !_isOpen;
        Notify();
    }

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        Notify();
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        Notify();
    }

    public void AddStep(Step step)
    {
        _steps.Add(ComposerItem.From(step));
        _isOpen = true;
        Notify();
    }

    public void RemoveStep(Guid id)
    {
        var idx = _steps.FindIndex(x => x.Id == id);
        if (idx < 0) return;
        _steps.RemoveAt(idx);
        Notify();
    }

    /// <summary>
    /// Updates a single parameter value on the identified item.
    /// No-op if the id, paramIndex, or value is invalid.
    /// </summary>
    public void SetParamValue(Guid itemId, int paramIndex, string value)
    {
        var idx = _steps.FindIndex(x => x.Id == itemId);
        if (idx < 0) return;
        var item = _steps[idx];
        if (paramIndex < 0 || paramIndex >= item.ParamValues.Count) return;
        var updated = new List<string>(item.ParamValues) { [paramIndex] = value };
        _steps[idx] = item with { ParamValues = updated };
        Notify();
    }

    public void MoveStep(Guid id, int targetIndex)
    {
        var idx = _steps.FindIndex(x => x.Id == id);
        if (idx < 0 || idx == targetIndex) return;
        var item = _steps[idx];
        _steps.RemoveAt(idx);
        _steps.Insert(Math.Clamp(targetIndex, 0, _steps.Count), item);
        Notify();
    }

    public void SetScenarioName(string name)
    {
        _scenarioName = name;
        Notify();
    }

    public void Clear()
    {
        _steps.Clear();
        _scenarioName = "";
        Notify();
    }

    private void Notify() { _featureTextCache = null; Changed?.Invoke(); }

    // Called when the DI root scope is disposed (app shutdown).
    // Components that subscribe to Changed must unsubscribe themselves via their own IDisposable.
    public void Dispose()
    {
        _actions.ToggleComposerRequested -= Toggle;
        _actions.CloseOverlayRequested   -= Close;
    }
}
