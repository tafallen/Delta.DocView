using Delta.DocView.Shared.Models;

namespace Delta.DocView.Client.Services;

public sealed class ComposerState : IDisposable
{
    private readonly IKeyboardActions _actions;
    private readonly List<ComposerItem> _steps = [];
    private bool _isOpen;
    private string _scenarioName = "";

    public IReadOnlyList<ComposerItem> Steps => _steps;
    public bool IsOpen => _isOpen;
    public string ScenarioName => _scenarioName;
    public int StepCount => _steps.Count;
    public string FeatureText => FeatureTextBuilder.Build(_scenarioName, _steps);

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
        Changed?.Invoke();
    }

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        Changed?.Invoke();
    }

    public void AddStep(Step step)
    {
        _steps.Add(ComposerItem.From(step));
        _isOpen = true;
        Changed?.Invoke();
    }

    public void RemoveStep(Guid id)
    {
        var idx = _steps.FindIndex(x => x.Id == id);
        if (idx < 0) return;
        _steps.RemoveAt(idx);
        Changed?.Invoke();
    }

    public void MoveStep(Guid id, int targetIndex)
    {
        var idx = _steps.FindIndex(x => x.Id == id);
        if (idx < 0 || idx == targetIndex) return;
        var item = _steps[idx];
        _steps.RemoveAt(idx);
        _steps.Insert(Math.Clamp(targetIndex, 0, _steps.Count), item);
        Changed?.Invoke();
    }

    public void SetScenarioName(string name)
    {
        _scenarioName = name;
        Changed?.Invoke();
    }

    public void Clear()
    {
        _steps.Clear();
        _scenarioName = "";
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _actions.ToggleComposerRequested -= Toggle;
        _actions.CloseOverlayRequested   -= Close;
    }
}
