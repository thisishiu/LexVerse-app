using System.Runtime.InteropServices;
using LexVerse.Core.Capture;
using LexVerse.Core.Windows;

namespace LexVerse.Infrastructure.Windows;

/// <summary>Session-local opaque IDs. Refresh invalidates all previous selections.</summary>
public sealed class WindowTargetRegistry
{
    private readonly Dictionary<string, WindowCaptureTarget> _targets = [];
    private readonly IWindowEnumerator _enumerator;
    private readonly Func<WindowCandidate, WindowCaptureTarget?> _identify;
    private readonly Func<WindowCaptureTarget, bool> _isCurrent;

    public WindowTargetRegistry(IWindowEnumerator? enumerator = null,
        Func<WindowCandidate, WindowCaptureTarget?>? identify = null,
        Func<WindowCaptureTarget, bool>? isCurrent = null)
    {
        _enumerator = enumerator ?? new Win32WindowEnumerator();
        _identify = identify ?? Identify;
        _isCurrent = isCurrent ?? IsCurrent;
    }

    private static WindowCaptureTarget? Identify(WindowCandidate window)
    {
        var thread = GetWindowThreadProcessId(window.Hwnd, out var process);
        return thread == 0 ? null : new(window.Hwnd, process, thread, window.Title);
    }

    public IReadOnlyList<WindowPickerEntry> Refresh()
    {
        _targets.Clear();
        var entries = new List<WindowPickerEntry>();
        foreach (var window in _enumerator.EnumerateWindows())
        {
            var target = _identify(window);
            if (target is null) continue;
            var id = Guid.NewGuid().ToString("N");
            _targets[id] = target;
            entries.Add(new(id, window.Title, window.ProcessName));
        }
        return entries;
    }

    public WindowCaptureTarget Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !_targets.TryGetValue(id, out var target) || !_isCurrent(target))
            throw new InvalidOperationException("The selected window is no longer available. Refresh the window list.");
        return target;
    }

    public static bool IsCurrent(WindowCaptureTarget target) =>
        IsWindow(target.Hwnd) &&
        GetWindowThreadProcessId(target.Hwnd, out var process) == target.ThreadId &&
        process == target.ProcessId;

    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}

public sealed record WindowPickerEntry(string Id, string Title, string? ProcessName);
