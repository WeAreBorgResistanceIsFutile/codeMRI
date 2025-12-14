using System;
using codeMRI.Server.Api;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace codeMRI.Frontend.Services;

public class AppState
{
    public string RepoPath { get; private set; } = "";
    public WikiStructure? Structure { get; private set; }
    public WikiPage? CurrentPage { get; private set; }
    public bool IsBusy { get; private set; }
    public string BusyMessage { get; private set; } = "Processing...";

    public event Action? OnChange;

    public void SetRepoPath(string path)
    {
        RepoPath = path;
        NotifyStateChanged();
    }

    public void SetStructure(WikiStructure structure)
    {
        Structure = structure;
        NotifyStateChanged();
    }

    public void SetCurrentPage(WikiPage page)
    {
        CurrentPage = page;
        NotifyStateChanged();
    }
    
    public void SetBusy(bool busy, string message = "Processing...")
    {
        IsBusy = busy;
        BusyMessage = message;
        NotifyStateChanged();
    }

    public ProgressInfo? CurrentProgress { get; private set; }

    public void SetProgress(ProgressInfo? info)
    {
        CurrentProgress = info;
        NotifyStateChanged();
    }

    public List<ChatMessage> ChatHistory { get; private set; } = new();

    public void AddChatMessage(ChatMessage msg)
    {
        ChatHistory.Add(msg);
        NotifyStateChanged();
    }

    public void UpdateLastChatMessage(string newContent)
    {
        if (ChatHistory.Any())
        {
            ChatHistory.Last().Content = newContent;
            NotifyStateChanged();
        }
    }

    // --- Agent Visualization State ---

    public Dictionary<string, AgentStatusEvent> ActiveAgents { get; private set; } = new();
    public List<DelegationEvent> DelegationHistory { get; private set; } = new();
    public List<TaskLifecycleEvent> TaskEvents { get; private set; } = new();

    private readonly ILogger<AppState> _logger;

    public AppState(ILogger<AppState> logger)
    {
        _logger = logger;
    }

    public void UpdateAgentStatus(AgentMessage msg)
    {
        try 
        {
            var content = Deserialize<AgentStatusEvent>(msg.Content);
            if (content != null)
            {
                // Key by Agent ID (SenderId)
                ActiveAgents[msg.SenderId] = content;
                NotifyStateChanged();
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error updating agent status: {Message}", ex.Message);
        }
    }

    public void AddDelegationEvent(AgentMessage msg)
    {
         try 
        {
            var content = Deserialize<DelegationEvent>(msg.Content);
            if (content != null)
            {
                DelegationHistory.Add(content);
                NotifyStateChanged();
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error adding delegation event: {Message}", ex.Message);
        }
    }

    public void AddTaskLifecycleEvent(AgentMessage msg)
    {
        try 
        {
            var content = Deserialize<TaskLifecycleEvent>(msg.Content);
            if (content != null)
            {
                TaskEvents.Add(content);
                NotifyStateChanged();
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error adding task lifecycle event: {Message}", ex.Message);
        }
    }

    private T? Deserialize<T>(object? content)
    {
        if (content is JsonElement element)
        {
            return element.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        return default;
    }

    // --- Repository Metadata ---
    
    public RepositoryStatusResponse? RepositoryStatus { get; private set; }
    
    public void SetRepositoryStatus(RepositoryStatusResponse? status)
    {
        RepositoryStatus = status;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
