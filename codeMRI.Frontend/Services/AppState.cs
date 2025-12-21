using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using codeMRI.Core.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Agents.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Frontend.Services;

public class AppState
{
    private readonly ILogger<AppState> _logger;

    public AppState(ILogger<AppState> logger)
    {
        _logger = logger;
    }

    public string RepoPath { get; private set; } = "";
    public WikiStructure? Structure { get; private set; }
    public WikiPage? CurrentPage { get; private set; }
    public bool IsBusy { get; private set; }
    public string BusyMessage { get; private set; } = "Processing...";

    public ProgressInfo? CurrentProgress { get; private set; }

    public List<ChatMessage> ChatHistory { get; } = new();

    // --- Agent Visualization State ---

    public Dictionary<string, AgentStatusEvent> ActiveAgents { get; } = new();
    public List<DelegationEvent> DelegationHistory { get; } = new();
    public List<TaskLifecycleEvent> TaskEvents { get; } = new();

    // --- Repository Metadata ---

    public RepositoryStatusResponse? RepositoryStatus { get; private set; }

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

    public void SetProgress(ProgressInfo? info)
    {
        CurrentProgress = info;
        NotifyStateChanged();
    }

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
            return element.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return default;
    }

    public void SetRepositoryStatus(RepositoryStatusResponse? status)
    {
        RepositoryStatus = status;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnChange?.Invoke();
    }
}