using Microsoft.AspNetCore.SignalR;
using codeMRI.Core.Models;

namespace codeMRI.Server.Hubs;

public class WikiHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
