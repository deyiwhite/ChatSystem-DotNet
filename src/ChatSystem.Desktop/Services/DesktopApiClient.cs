using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChatSystem.Desktop.Models;

namespace ChatSystem.Desktop.Services;

public sealed class DesktopApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;

    public DesktopApiClient(string serverUrl)
    {
        _serverUrl = NormalizeServerUrl(serverUrl);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_serverUrl),
            // 文件上传/下载可能较大，超时放宽；服务器未启动时连接会被立即拒绝，不受此影响。
            Timeout = TimeSpan.FromSeconds(100)
        };
    }

    public DesktopApiClient(string serverUrl, string token = null)
    {
        _serverUrl = NormalizeServerUrl(serverUrl);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_serverUrl),
            Timeout = TimeSpan.FromSeconds(100)
        };

        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(username, password),
                JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(BuildConnectionErrorMessage(_serverUrl), ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException($"连接服务器超时：{_serverUrl}。请确认 Web 服务已经启动。", ex);
        }

        await EnsureSuccessAsync(response);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        if (loginResponse is null)
        {
            throw new InvalidOperationException("登录接口没有返回有效数据。");
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResponse.Token);

        return loginResponse;
    }

    public void SetToken(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<List<FriendItem>> GetFriendsAsync()
    {
        var response = await _httpClient.GetAsync("/api/friends");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<FriendItem>>(JsonOptions)
               ?? new List<FriendItem>();
    }

    public async Task<List<FriendRequestItem>> GetFriendRequestsAsync()
    {
        var response = await _httpClient.GetAsync("/api/friends/requests");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<FriendRequestItem>>(JsonOptions)
               ?? new List<FriendRequestItem>();
    }

    public async Task AcceptFriendRequestAsync(int requestId)
    {
        var response = await _httpClient.PostAsync($"/api/friends/requests/{requestId}/accept", null);
        await EnsureSuccessAsync(response);
    }

    public async Task RejectFriendRequestAsync(int requestId)
    {
        var response = await _httpClient.PostAsync($"/api/friends/requests/{requestId}/reject", null);
        await EnsureSuccessAsync(response);
    }

    public async Task<List<SearchUserItem>> SearchUsersAsync(string keyword)
    {
        var response = await _httpClient.GetAsync($"/api/friends/search?keyword={Uri.EscapeDataString(keyword)}");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<SearchUserItem>>(JsonOptions)
               ?? new List<SearchUserItem>();
    }

    public async Task SendFriendRequestAsync(int toUserId)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/friends/requests",
            new SendFriendRequestRequest(toUserId),
            JsonOptions);

        await EnsureSuccessAsync(response);
    }

    public async Task DeleteFriendAsync(int friendId)
    {
        var response = await _httpClient.DeleteAsync($"/api/friends/{friendId}");
        await EnsureSuccessAsync(response);
    }

    public async Task<List<HistoryMessageItem>> GetHistoryMessagesAsync(int? friendId = null, string? keyword = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var queryParams = new List<string>();

        if (friendId.HasValue)
            queryParams.Add($"friendId={friendId.Value}");

        if (!string.IsNullOrWhiteSpace(keyword))
            queryParams.Add($"keyword={Uri.EscapeDataString(keyword)}");

        if (startDate.HasValue)
            queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");

        if (endDate.HasValue)
            queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

        var queryString = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : "";
        var response = await _httpClient.GetAsync($"/api/messages/history{queryString}");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<HistoryMessageItem>>(JsonOptions)
               ?? new List<HistoryMessageItem>();
    }

    public async Task DeleteHistoryMessageAsync(int messageId)
    {
        var response = await _httpClient.DeleteAsync($"/api/messages/history/{messageId}");
        await EnsureSuccessAsync(response);
    }

    public async Task<List<GroupMessageItem>> GetGroupHistoryMessagesAsync(int? groupId = null, string? keyword = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var queryParams = new List<string>();

        if (groupId.HasValue)
            queryParams.Add($"groupId={groupId.Value}");

        if (!string.IsNullOrWhiteSpace(keyword))
            queryParams.Add($"keyword={Uri.EscapeDataString(keyword)}");

        if (startDate.HasValue)
            queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");

        if (endDate.HasValue)
            queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

        var queryString = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : "";
        var response = await _httpClient.GetAsync($"/api/groups/history{queryString}");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<GroupMessageItem>>(JsonOptions)
               ?? new List<GroupMessageItem>();
    }

    public async Task<List<MessageItem>> GetMessagesAsync(int friendId)
    {
        var response = await _httpClient.GetAsync($"/api/messages/{friendId}");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<MessageItem>>(JsonOptions)
               ?? new List<MessageItem>();
    }

    public async Task<MessageItem> SendMessageAsync(int toUserId, string content)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/messages",
            new SendMessageRequest(toUserId, content),
            JsonOptions);

        await EnsureSuccessAsync(response);
        var message = await response.Content.ReadFromJsonAsync<MessageItem>(JsonOptions);
        if (message is null)
        {
            throw new InvalidOperationException("发送消息接口没有返回有效数据。");
        }

        return message;
    }

    public async Task<MessageItem> SendPrivateFileAsync(int toUserId, string filePath)
    {
        using var form = BuildFileForm(filePath, "toUserId", toUserId);
        var response = await _httpClient.PostAsync("/api/files/private", form);
        await EnsureSuccessAsync(response);

        var message = await response.Content.ReadFromJsonAsync<MessageItem>(JsonOptions);
        if (message is null)
        {
            throw new InvalidOperationException("发送文件接口没有返回有效数据。");
        }

        return message;
    }

    public async Task<GroupMessageItem> SendGroupFileAsync(int groupId, string filePath)
    {
        using var form = BuildFileForm(filePath, "groupId", groupId);
        var response = await _httpClient.PostAsync("/api/files/group", form);
        await EnsureSuccessAsync(response);

        var message = await response.Content.ReadFromJsonAsync<GroupMessageItem>(JsonOptions);
        if (message is null)
        {
            throw new InvalidOperationException("发送文件接口没有返回有效数据。");
        }

        return message;
    }

    public async Task DownloadFileAsync(string downloadUrl, string savePath)
    {
        var response = await _httpClient.GetAsync(downloadUrl);
        await EnsureSuccessAsync(response);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(savePath, bytes);
    }

    public async Task<List<GroupItem>> GetGroupsAsync()
    {
        var response = await _httpClient.GetAsync("/api/groups");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<GroupItem>>(JsonOptions)
               ?? new List<GroupItem>();
    }

    public async Task<GroupItem> CreateGroupAsync(string name, List<int> memberIds)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/groups",
            new CreateGroupRequest(name, memberIds),
            JsonOptions);

        await EnsureSuccessAsync(response);
        var group = await response.Content.ReadFromJsonAsync<GroupItem>(JsonOptions);
        if (group is null)
        {
            throw new InvalidOperationException("创建群聊接口没有返回有效数据。");
        }

        return group;
    }

    public async Task<List<GroupMemberApiModel>> GetGroupMembersAsync(int groupId)
    {
        var response = await _httpClient.GetAsync($"/api/groups/{groupId}/members");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<GroupMemberApiModel>>(JsonOptions)
               ?? new List<GroupMemberApiModel>();
    }

    public async Task AddGroupMemberAsync(int groupId, int userId)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/groups/{groupId}/members",
            new AddMemberRequest(userId),
            JsonOptions);

        await EnsureSuccessAsync(response);
    }

    public async Task LeaveGroupAsync(int groupId)
    {
        var response = await _httpClient.PostAsync($"/api/groups/{groupId}/leave", null);
        await EnsureSuccessAsync(response);
    }

    public async Task DisbandGroupAsync(int groupId)
    {
        var response = await _httpClient.DeleteAsync($"/api/groups/{groupId}");
        await EnsureSuccessAsync(response);
    }

    public async Task<List<GroupMessageItem>> GetGroupMessagesAsync(int groupId)
    {
        var response = await _httpClient.GetAsync($"/api/groups/{groupId}/messages");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<GroupMessageItem>>(JsonOptions)
               ?? new List<GroupMessageItem>();
    }

    public async Task<GroupMessageItem> SendGroupTextAsync(int groupId, string content)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/groups/{groupId}/messages",
            new SendGroupTextRequest(content),
            JsonOptions);

        await EnsureSuccessAsync(response);
        var message = await response.Content.ReadFromJsonAsync<GroupMessageItem>(JsonOptions);
        if (message is null)
        {
            throw new InvalidOperationException("发送群消息接口没有返回有效数据。");
        }

        return message;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static MultipartFormDataContent BuildFileForm(string filePath, string idFieldName, int idValue)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("找不到要发送的文件。");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            throw new InvalidOperationException("不能发送空文件。");
        }

        if (fileInfo.Length > 20L * 1024 * 1024)
        {
            throw new InvalidOperationException("文件超过 20 MB 上限。");
        }

        var form = new MultipartFormDataContent
        {
            { new StringContent(idValue.ToString()), idFieldName }
        };

        var fileContent = new StreamContent(File.OpenRead(filePath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        return form;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                throw new InvalidOperationException(error.Message);
            }
        }
        catch (JsonException)
        {
            // 响应不是 JSON 错误体，落到下面的通用错误。
        }

        throw new InvalidOperationException($"请求失败：{(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private static string BuildConnectionErrorMessage(string serverUrl)
    {
        return $"无法连接服务器：{serverUrl}。请先启动 ChatSystem.Server，并确认浏览器能打开 {serverUrl}。";
    }

    private static string NormalizeServerUrl(string serverUrl)
    {
        var normalized = serverUrl.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "http://127.0.0.1:5098";
        }

        return normalized.TrimEnd('/');
    }
}
