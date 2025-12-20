using codeMRI.Server.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace codeMRI.Frontend.Services;

public class WikiHubClient : IAsyncDisposable
{
    private readonly NavigationManager _navigation;
    private HubConnection? _hubConnection;

    public WikiHubClient(NavigationManager navigation)
    {
        _navigation = navigation;
    }

    public string? ConnectionId => _hubConnection?.ConnectionId;

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }

    public event Action<ProgressInfo>? OnProgress;
    public event Action<AgentMessage>? OnAgentStatus;
    public event Action<AgentMessage>? OnDelegation;
    public event Action<AgentMessage>? OnTaskLifecycle;

    public async Task StartAsync()
    {
        if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected) return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigation.ToAbsoluteUri("/wikiHub"))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<ProgressInfo>("ReceiveProgress", info => OnProgress?.Invoke(info));

        _hubConnection.On<AgentMessage>("ReceiveAgentStatus", msg => OnAgentStatus?.Invoke(msg));
        _hubConnection.On<AgentMessage>("ReceiveDelegationEvent", msg => OnDelegation?.Invoke(msg));
        _hubConnection.On<AgentMessage>("ReceiveTaskLifecycle", msg => OnTaskLifecycle?.Invoke(msg));

        await _hubConnection.StartAsync();
    }
}