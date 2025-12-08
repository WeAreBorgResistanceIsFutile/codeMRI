using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

public class ProgressService : IProgressService
{
    private Action<ProgressInfo>? _handler;
    
    // Stack of scaling contexts.
    // Each context knows its parent's range, so we can calculate the absolute global percentage.
    private readonly Stack<ScalingContext> _contextStack = new();

    private class ScalingContext
    {
        public double Start { get; set; }
        public double Width { get; set; }
    }

    public ProgressService()
    {
        // Root context covers 0-100%
        _contextStack.Push(new ScalingContext { Start = 0, Width = 100 });
    }

    public void SetHandler(Action<ProgressInfo> handler)
    {
        _handler = handler;
    }

    public void Report(ProgressInfo info)
    {
        if (_handler == null) return;

        var context = _contextStack.Peek();
        
        // Calculate global percentage based on current context
        // context.Width is the width in the PARENT's terms? No, let's keep it global.
        // It's easier if contexts always store GLOBAL ranges.
        
        // Local: 50%
        // Context: Start=10, Width=20 (meaning 10% to 30% global)
        // Global = 10 + (50/100 * 20) = 20%
        
        var globalPct = context.Start + ((double)info.Percentage / 100.0 * context.Width);
        
        _handler(new ProgressInfo
        {
            Phase = info.Phase,
            Message = info.Message,
            Percentage = (int)globalPct
        });
    }

    public async Task WithScalingAsync(double relativeStart, double relativeWidth, Func<Task> operation)
    {
        var parent = _contextStack.Peek();
        
        // Convert relative range to global range
        // If parent is 10-30 (Start=10, Width=20)
        // And we ask for 0-50% relative to parent (Start=0, Width=50)
        // Global Start = 10 + (0/100 * 20) = 10
        // Global Width = (50/100 * 20) = 10
        // New Context: 10-20
        
        var globalStart = parent.Start + (relativeStart / 100.0 * parent.Width);
        var globalWidth = (relativeWidth / 100.0 * parent.Width);
        
        _contextStack.Push(new ScalingContext { Start = globalStart, Width = globalWidth });
        
        try
        {
            await operation();
        }
        finally
        {
            _contextStack.Pop();
        }
    }
}
