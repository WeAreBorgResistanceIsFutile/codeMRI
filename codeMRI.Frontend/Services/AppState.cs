using System;
using codeMRI.Server.Api;

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

    private void NotifyStateChanged() => OnChange?.Invoke();
}
