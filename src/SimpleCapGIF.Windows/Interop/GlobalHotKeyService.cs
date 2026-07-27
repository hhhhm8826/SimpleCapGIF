using System.Windows.Interop;
using SimpleCapGIF.Core.Models;

namespace SimpleCapGIF.Windows.Interop;

public enum GlobalHotKeyAction
{
    ToggleRecording,
    CancelRecording,
}

public sealed class GlobalHotKeyService : IDisposable
{
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModNoRepeat = 0x4000;
    internal const uint VirtualKeyF9 = 0x78;
    internal const uint VirtualKeyF12 = 0x7B;
    internal const uint VirtualKeyR = 0x52;

    private const int WmHotKey = 0x0312;
    private const int ToggleRecordingId = 0x5343;
    private const int CancelRecordingId = 0x5344;
    private const int AlternateToggleRecordingId = 0x5345;
    private readonly nint _window;
    private readonly HwndSource _source;
    private GlobalHotKeyPreset _togglePreset = GlobalHotKeyPreset.Disabled;
    private int _toggleRegistrationId = ToggleRecordingId;
    private bool _cancelRegistered;
    private bool _disposed;

    public GlobalHotKeyService(nint window)
    {
        _window = window != 0 ? window : throw new ArgumentException("A valid window handle is required.", nameof(window));
        _source = HwndSource.FromHwnd(window) ?? throw new InvalidOperationException("The window source is unavailable.");
        _source.AddHook(WndProc);
    }

    public event Action<GlobalHotKeyAction>? Pressed;

    public GlobalHotKeyPreset TogglePreset => _togglePreset;
    public bool IsCancelRegistered => _cancelRegistered;

    public bool TrySetToggle(GlobalHotKeyPreset preset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enum.IsDefined(preset)) throw new ArgumentOutOfRangeException(nameof(preset));
        if (_togglePreset == preset) return true;

        if (preset == GlobalHotKeyPreset.Disabled)
        {
            if (_togglePreset != GlobalHotKeyPreset.Disabled)
            {
                NativeMethods.UnregisterHotKey(_window, _toggleRegistrationId);
            }

            _togglePreset = GlobalHotKeyPreset.Disabled;
            return true;
        }

        var binding = GetBinding(preset);
        var candidateId = _toggleRegistrationId == ToggleRecordingId ? AlternateToggleRecordingId : ToggleRecordingId;
        if (!NativeMethods.RegisterHotKey(_window, candidateId, binding.Modifiers | ModNoRepeat, binding.VirtualKey)) return false;

        if (_togglePreset != GlobalHotKeyPreset.Disabled)
        {
            NativeMethods.UnregisterHotKey(_window, _toggleRegistrationId);
        }

        _toggleRegistrationId = candidateId;
        _togglePreset = preset;
        return true;
    }

    public bool TryRegisterCancel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cancelRegistered) return true;
        _cancelRegistered = NativeMethods.RegisterHotKey(
            _window,
            CancelRecordingId,
            ModControl | ModShift | ModNoRepeat,
            VirtualKeyF12);
        return _cancelRegistered;
    }

    internal static (uint Modifiers, uint VirtualKey) GetBinding(GlobalHotKeyPreset preset) => preset switch
    {
        GlobalHotKeyPreset.F12 => (0, VirtualKeyF12),
        GlobalHotKeyPreset.AltF9 => (ModAlt, VirtualKeyF9),
        GlobalHotKeyPreset.ControlShiftR => (ModControl | ModShift, VirtualKeyR),
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != WmHotKey) return 0;
        var id = unchecked((int)wParam);
        if (id == _toggleRegistrationId && _togglePreset != GlobalHotKeyPreset.Disabled)
        {
            handled = true;
            Pressed?.Invoke(GlobalHotKeyAction.ToggleRecording);
        }
        else if (id == CancelRecordingId)
        {
            handled = true;
            Pressed?.Invoke(GlobalHotKeyAction.CancelRecording);
        }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_togglePreset != GlobalHotKeyPreset.Disabled)
        {
            NativeMethods.UnregisterHotKey(_window, _toggleRegistrationId);
            _togglePreset = GlobalHotKeyPreset.Disabled;
        }

        if (_cancelRegistered)
        {
            NativeMethods.UnregisterHotKey(_window, CancelRecordingId);
            _cancelRegistered = false;
        }

        _source.RemoveHook(WndProc);
    }
}
