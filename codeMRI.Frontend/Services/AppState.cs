using System;
using System.Collections.Generic;
using codeMRI.Server.Api;

namespace codeMRI.Frontend.Services
{
    public class AppState
    {
        // State properties
        public string RepoPath { get; private set; } = ""; // Default empty
        public bool IsBusy { get; private set; }
        public string BusyMessage { get; private set; } = "";
        
        public WikiStructure? Structure { get; private set; }
        public WikiPage? CurrentPage { get; private set; }
        
        public List<ChatMessage> ChatHistory { get; private set; } = new();

        // Event for state changes
        public event Action? OnChange;

        // State modification methods
        public void SetRepoPath(string path)
        {
            if (RepoPath != path)
            {
                RepoPath = path;
                NotifyStateChanged();
            }
        }

        public void SetBusy(bool isBusy, string message = "")
        {
            IsBusy = isBusy;
            BusyMessage = message;
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

        public void AddChatMessage(ChatMessage message)
        {
            ChatHistory.Add(message);
            NotifyStateChanged();
        }

        public void UpdateLastChatMessage(string content)
        {
            if (ChatHistory.Count > 0)
            {
                var lastMsg = ChatHistory[ChatHistory.Count - 1];
                // Since ChatMessage is a class, we can modify it directly if it's mutable. 
                // Assuming it is based on previous file reads.
                lastMsg.Content = content;
                NotifyStateChanged();
            }
        }
        
        public void ClearChat()
        {
            ChatHistory.Clear();
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
