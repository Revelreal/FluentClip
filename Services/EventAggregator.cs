using System;

namespace FluentClip.Services;

public interface IEventAggregator
{
    event Action<string>? OnStreamingResponse;
    event Action? OnComplete;
    event Action<string>? OnError;
    event Action<string>? OnToolCallStart;
    event Action? OnToolCallComplete;
    event Action<int, int>? OnTokenUsageUpdated;
    event Action<string>? OnContextSummarized;
    event Action<string>? OnShellOutput;
    event Action<string>? OnShellError;
    event Action? OnShellComplete;

    void PublishStreamingResponse(string chunk);
    void PublishComplete();
    void PublishError(string error);
    void PublishToolCallStart(string toolName);
    void PublishToolCallComplete();
    void PublishTokenUsageUpdated(int used, int max);
    void PublishContextSummarized(string summary);
    void PublishShellOutput(string output);
    void PublishShellError(string error);
    void PublishShellComplete();
}

public class EventAggregator : IEventAggregator
{
    public event Action<string>? OnStreamingResponse;
    public event Action? OnComplete;
    public event Action<string>? OnError;
    public event Action<string>? OnToolCallStart;
    public event Action? OnToolCallComplete;
    public event Action<int, int>? OnTokenUsageUpdated;
    public event Action<string>? OnContextSummarized;
    public event Action<string>? OnShellOutput;
    public event Action<string>? OnShellError;
    public event Action? OnShellComplete;

    public void PublishStreamingResponse(string chunk) => OnStreamingResponse?.Invoke(chunk);
    public void PublishComplete() => OnComplete?.Invoke();
    public void PublishError(string error) => OnError?.Invoke(error);
    public void PublishToolCallStart(string toolName) => OnToolCallStart?.Invoke(toolName);
    public void PublishToolCallComplete() => OnToolCallComplete?.Invoke();
    public void PublishTokenUsageUpdated(int used, int max) => OnTokenUsageUpdated?.Invoke(used, max);
    public void PublishContextSummarized(string summary) => OnContextSummarized?.Invoke(summary);
    public void PublishShellOutput(string output) => OnShellOutput?.Invoke(output);
    public void PublishShellError(string error) => OnShellError?.Invoke(error);
    public void PublishShellComplete() => OnShellComplete?.Invoke();
}
