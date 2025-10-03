using System;
using System.Runtime.InteropServices;
using System.Timers; // still import for ElapsedEventArgs, will fully qualify Timer below

namespace Teine64;

/// <summary>
/// Manages preventing the system from sleeping / turning off display using SetThreadExecutionState.
/// Periodically refreshes the state to be conservative on some systems.
/// </summary>
internal sealed class AwakeService : IDisposable
{
    private readonly System.Timers.Timer _refreshTimer;
    private bool _enabled;
    private bool _disposed;

    [Flags]
    private enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001,
        // Legacy flag - not used: 0x00000004 (ES_USER_PRESENT)
    }

    [DllImport("kernel32.dll")]
    private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    public AwakeService(bool enabled)
    {
        _enabled = enabled;
    _refreshTimer = new System.Timers.Timer(25_000); // refresh every 25s
        _refreshTimer.Elapsed += (_, _) => Apply();
        _refreshTimer.AutoReset = true;
        _refreshTimer.Enabled = true;
        Apply();
    }

    public bool Enabled => _enabled;

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        _enabled = enabled;
        Apply();
    }

    private void Apply()
    {
        if (_disposed) return;
        if (_enabled)
        {
            // Keep display & system awake continuously.
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
        }
        else
        {
            // Clear requirements but continue state.
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Dispose();
        // Clear on dispose just once more.
        try { SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS); } catch { /* ignore */ }
        GC.SuppressFinalize(this);
    }
}