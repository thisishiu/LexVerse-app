namespace LexVerse.Core.Capture;

/// <summary>Native-only identity, never accepted as a handle from the web UI.</summary>
public sealed record WindowCaptureTarget(IntPtr Hwnd, uint ProcessId, uint ThreadId, string DisplayName);
