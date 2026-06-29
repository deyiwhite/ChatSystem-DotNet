using ChatSystem.Desktop.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ChatSystem.Desktop.Services;

public sealed class SignalRService : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public SignalRService(string serverUrl, string token)
    {
        var hubUrl = $"{serverUrl.Trim().TrimEnd('/')}/chatHub";
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .AddJsonProtocol(options =>
            {
                // 服务端推送的 JSON 大小写不固定，开启大小写不敏感更稳妥。
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .WithAutomaticReconnect()
            .Build();
    }

    public event Action<MessageItem>? MessageReceived;

    public event Action<GroupMessageItem>? GroupMessageReceived;

    public HubConnectionState State => _connection.State;

    public async Task ConnectAsync()
    {
        _connection.On<MessageItem>("ReceivePrivateMessage", message =>
        {
            MessageReceived?.Invoke(message);
        });

        _connection.On<GroupMessageItem>("ReceiveGroupMessage", message =>
        {
            GroupMessageReceived?.Invoke(message);
        });

        await _connection.StartAsync();
    }

    public async Task SendPrivateMessageAsync(int toUserId, string content)
    {
        await _connection.InvokeAsync("SendPrivateMessage", toUserId, content);
    }

    public async Task SendGroupMessageAsync(int groupId, string content)
    {
        await _connection.InvokeAsync("SendGroupMessage", groupId, content);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
