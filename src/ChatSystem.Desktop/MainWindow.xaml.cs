using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChatSystem.Desktop.Models;
using ChatSystem.Desktop.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;

namespace ChatSystem.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<FriendViewModel> _friends = new();
    private readonly ObservableCollection<FriendRequestItem> _friendRequests = new();
    private readonly ObservableCollection<GroupViewModel> _groups = new();
    private readonly ObservableCollection<ChatMessageViewModel> _messages = new();
    private readonly LocalSettingsService _settingsService = new();

    private DesktopApiClient? _apiClient;
    private SignalRService? _signalRService;
    private LoginResponse? _currentUser;
    private FriendViewModel? _selectedFriend;
    private GroupViewModel? _selectedGroup;
    private List<GroupMemberApiModel> _selectedGroupMembers = new();
    private bool _isSyncingPassword;
    private string? _serverUrl;
    private string? _token;

    public MainWindow()
    {
        InitializeComponent();
        FriendRequestsList.ItemsSource = _friendRequests;
        FriendsList.ItemsSource = _friends;
        GroupsList.ItemsSource = _groups;
        MessagesList.ItemsSource = _messages;
        ShowLoginView();
        _ = LoadSavedSettingsAsync();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await LoginAsync();
    }

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await LoginAsync();
        }
    }

    private async void VisiblePasswordTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await LoginAsync();
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncingPassword)
        {
            return;
        }

        _isSyncingPassword = true;
        VisiblePasswordTextBox.Text = PasswordBox.Password;
        _isSyncingPassword = false;
    }

    private void VisiblePasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingPassword)
        {
            return;
        }

        _isSyncingPassword = true;
        PasswordBox.Password = VisiblePasswordTextBox.Text;
        _isSyncingPassword = false;
    }

    private void ShowPasswordCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var showPassword = ShowPasswordCheckBox.IsChecked == true;
        if (showPassword)
        {
            VisiblePasswordTextBox.Text = PasswordBox.Password;
            VisiblePasswordTextBox.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
            VisiblePasswordTextBox.Focus();
            VisiblePasswordTextBox.CaretIndex = VisiblePasswordTextBox.Text.Length;
            return;
        }

        PasswordBox.Password = VisiblePasswordTextBox.Text;
        PasswordBox.Visibility = Visibility.Visible;
        VisiblePasswordTextBox.Visibility = Visibility.Collapsed;
        PasswordBox.Focus();
    }

    private async Task LoginAsync()
    {
        // 显示加载状态
        LoginButton.IsEnabled = false;
        LoginButton.Content = "登录中...";
        LoginButton.Background = new SolidColorBrush(Color.FromRgb(216, 180, 254)); // 浅紫色
        LoginButton.FontWeight = FontWeights.SemiBold;

        SetLoginStatus("正在连接到服务器...", false);

        try
        {
            await DisconnectSignalRAsync();
            _apiClient?.Dispose();
            ClearChatState();

            _serverUrl = ServerUrlTextBox.Text;
            _apiClient = new DesktopApiClient(_serverUrl);
            _currentUser = await _apiClient.LoginAsync(UsernameTextBox.Text, PasswordBox.Password);
            _token = _currentUser.Token;

            await SaveLoginSettingsAsync();

            CurrentUserTextBlock.Text = FormatDisplayName(_currentUser.DisplayName, _currentUser.Username);
            ConnectionTextBlock.Text = "正在连接";

            await RefreshFriendsAsync();
            await RefreshFriendRequestsAsync();
            await RefreshGroupsAsync();

            _signalRService = new SignalRService(ServerUrlTextBox.Text, _currentUser.Token);
            _signalRService.MessageReceived += SignalRService_MessageReceived;
            _signalRService.GroupMessageReceived += SignalRService_GroupMessageReceived;
            await _signalRService.ConnectAsync();

            ConnectionTextBlock.Text = "已连接";
            StatusTextBlock.Text = _friends.Count == 0
                ? "登录成功，但当前没有好友。"
                : "登录成功，请选择好友或群聊开始交流。";

            if (_friendRequests.Count > 0)
            {
                StatusTextBlock.Text = $"登录成功，有 {_friendRequests.Count} 条好友申请待处理。";
            }

            ShowChatView();

            if (_friends.Count > 0)
            {
                FriendsList.SelectedIndex = 0;
            }

            // 恢复按钮状态
            ResetLoginButton();
        }
        catch (Exception ex)
        {
            await DisconnectSignalRAsync();
            _apiClient?.Dispose();
            _apiClient = null;

            // 恢复按钮状态并显示错误
            ResetLoginButton();
            ShowToastNotification($"登录失败: {ex.Message}", "error");
            _currentUser = null;
            ClearChatState();
            SetLoginStatus(ex.Message, true);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void FriendManagementButton_Click(object sender, RoutedEventArgs e)
    {
        if (_apiClient != null)
        {
            var friendWindow = new FriendManagementWindow(_serverUrl, _token, async () =>
            {
                // ★ 好友被删除时，在 UI 线程刷新好友列表，并清理聊天状态
                await Dispatcher.InvokeAsync(async () =>
                {
                    await RefreshFriendsAsync();

                    // 如果当前正在和已被删除的好友聊天，清空聊天区域
                    if (_selectedFriend != null && FindFriend(_selectedFriend.Id) == null)
                    {
                        _selectedFriend = null;
                        _messages.Clear();
                        SelectedFriendTextBlock.Text = "请选择好友或群聊";
                        ConversationSubtitle.Text = "消息会实时同步到 Web 端和桌面端";
                        SendButton.IsEnabled = false;
                        AttachFileButton.IsEnabled = false;
                        StatusTextBlock.Text = "好友已删除。";
                    }
                });
            });
            friendWindow.Owner = this;
            friendWindow.Show();
        }
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_apiClient != null)
        {
            var historyWindow = new HistoryWindow(_serverUrl, _token);
            historyWindow.Owner = this;
            historyWindow.Show();
        }
    }

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        await LogoutAsync();
    }

    private async Task LogoutAsync()
    {
        await DisconnectSignalRAsync();
        _apiClient?.Dispose();
        _apiClient = null;
        _currentUser = null;
        ClearChatState();
        await ApplyLogoutLoginFieldsAsync();
        ShowLoginView();
        SetLoginStatus("已退出登录。", false);
    }

    private async void RefreshRequestsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunWithStatusAsync("正在刷新好友申请...", async () =>
        {
            await RefreshFriendRequestsAsync();
            StatusTextBlock.Text = _friendRequests.Count == 0
                ? "当前没有待处理好友申请。"
                : $"已刷新，有 {_friendRequests.Count} 条好友申请待处理。";
        });
    }

    private async void AcceptRequestButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int requestId } || _apiClient is null)
        {
            return;
        }

        await RunWithStatusAsync("正在同意好友申请...", async () =>
        {
            await _apiClient.AcceptFriendRequestAsync(requestId);
            await RefreshFriendRequestsAsync();
            await RefreshFriendsAsync();
            StatusTextBlock.Text = "已同意好友申请，好友列表已刷新。";

            if (_friends.Count > 0 && FriendsList.SelectedItem is null && _selectedGroup is null)
            {
                FriendsList.SelectedIndex = 0;
            }
        });
    }

    private async void RejectRequestButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int requestId } || _apiClient is null)
        {
            return;
        }

        await RunWithStatusAsync("正在拒绝好友申请...", async () =>
        {
            await _apiClient.RejectFriendRequestAsync(requestId);
            await RefreshFriendRequestsAsync();
            StatusTextBlock.Text = "已拒绝好友申请。";
        });
    }

    private async void FriendsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FriendsList.SelectedItem is not FriendViewModel friend)
        {
            return;
        }

        GroupsList.SelectedItem = null;
        _selectedGroup = null;
        GroupActionsPanel.Visibility = Visibility.Collapsed;

        _selectedFriend = friend;
        _selectedFriend.UnreadCount = 0;
        SelectedFriendTextBlock.Text = FormatDisplayName(friend.DisplayName, friend.Username);
        ConversationSubtitle.Text = "私聊 · 消息实时同步";
        SendButton.IsEnabled = _currentUser is not null;
        AttachFileButton.IsEnabled = _currentUser is not null;

        await RunWithStatusAsync("正在加载历史消息...", async () =>
        {
            if (_apiClient is null)
            {
                return;
            }

            _messages.Clear();
            foreach (var message in await _apiClient.GetMessagesAsync(friend.Id))
            {
                AddPrivateMessageToView(message);
            }

            ScrollMessagesToEnd();
            StatusTextBlock.Text = $"已加载与 {friend.DisplayName} 的历史消息。";
        });
    }

    private async void GroupsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupsList.SelectedItem is not GroupViewModel group)
        {
            return;
        }

        FriendsList.SelectedItem = null;
        _selectedFriend = null;
        _selectedGroup = group;
        group.UnreadCount = 0;

        SelectedFriendTextBlock.Text = group.Name;
        ConversationSubtitle.Text = group.IsOwner ? $"{group.MemberCount} 位成员 · 我是群主" : $"{group.MemberCount} 位成员";
        GroupActionsPanel.Visibility = Visibility.Visible;
        LeaveOrDisbandButton.Content = group.IsOwner ? "解散群聊" : "退出群聊";
        SendButton.IsEnabled = _currentUser is not null;
        AttachFileButton.IsEnabled = _currentUser is not null;

        await RunWithStatusAsync("正在加载群消息...", async () =>
        {
            if (_apiClient is null)
            {
                return;
            }

            await LoadAddableMembersAsync(group);

            _messages.Clear();
            foreach (var message in await _apiClient.GetGroupMessagesAsync(group.Id))
            {
                AddGroupMessageToView(message);
            }

            ScrollMessagesToEnd();
            StatusTextBlock.Text = $"已进入群聊「{group.Name}」。";
        });
    }

    private async void NewGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_apiClient is null)
        {
            return;
        }

        if (_friends.Count == 0)
        {
            MessageBox.Show("请先添加好友，再创建群聊。", "新建群聊", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new NewGroupWindow(_friends.Select(friend => new FriendItem(friend.Id, friend.Username, friend.DisplayName)))
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await RunWithStatusAsync("正在创建群聊...", async () =>
        {
            var group = await _apiClient.CreateGroupAsync(dialog.GroupName, dialog.SelectedFriendIds);
            await RefreshGroupsAsync();
            StatusTextBlock.Text = $"已创建群聊「{group.Name}」。";
        });
    }

    private async void RefreshGroupsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunWithStatusAsync("正在刷新群聊...", async () =>
        {
            await RefreshGroupsAsync();
            StatusTextBlock.Text = "群聊列表已刷新。";
        });
    }

    private async void AddMemberButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup is null || _apiClient is null)
        {
            return;
        }

        if (AddMemberComboBox.SelectedValue is not int userId)
        {
            StatusTextBlock.Text = "请选择要添加的好友。";
            return;
        }

        var group = _selectedGroup;
        await RunWithStatusAsync("正在添加成员...", async () =>
        {
            await _apiClient.AddGroupMemberAsync(group.Id, userId);
            await RefreshGroupsAsync();
            StatusTextBlock.Text = "已添加群成员。";
        });
    }

    private async void LeaveOrDisbandButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup is null || _apiClient is null)
        {
            return;
        }

        var group = _selectedGroup;
        var isOwner = group.IsOwner;
        var prompt = isOwner
            ? $"确定解散群「{group.Name}」吗？该操作不可恢复。"
            : $"确定退出群「{group.Name}」吗？";

        if (MessageBox.Show(prompt, "群聊", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await RunWithStatusAsync(isOwner ? "正在解散群聊..." : "正在退出群聊...", async () =>
        {
            if (isOwner)
            {
                await _apiClient.DisbandGroupAsync(group.Id);
            }
            else
            {
                await _apiClient.LeaveGroupAsync(group.Id);
            }

            await RefreshGroupsAsync();
            StatusTextBlock.Text = isOwner ? "已解散群聊。" : "已退出群聊。";
        });
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendCurrentMessageAsync();
    }

    private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            await SendCurrentMessageAsync();
        }
    }

    private async Task SendCurrentMessageAsync()
    {
        if (_apiClient is null || _currentUser is null)
        {
            StatusTextBlock.Text = "请先登录。";
            return;
        }

        if (_selectedGroup is null && _selectedFriend is null)
        {
            StatusTextBlock.Text = "请先选择好友或群聊。";
            return;
        }

        var content = MessageTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            StatusTextBlock.Text = "消息内容不能为空。";
            return;
        }

        await RunWithStatusAsync("正在发送消息...", async () =>
        {
            SendButton.IsEnabled = false;
            var connected = _signalRService?.State == HubConnectionState.Connected;

            if (_selectedGroup is not null)
            {
                if (connected)
                {
                    await _signalRService!.SendGroupMessageAsync(_selectedGroup.Id, content);
                }
                else
                {
                    var message = await _apiClient.SendGroupTextAsync(_selectedGroup.Id, content);
                    AddGroupMessageToView(message);
                    ScrollMessagesToEnd();
                }
            }
            else
            {
                if (connected)
                {
                    await _signalRService!.SendPrivateMessageAsync(_selectedFriend!.Id, content);
                }
                else
                {
                    var message = await _apiClient.SendMessageAsync(_selectedFriend!.Id, content);
                    AddPrivateMessageToView(message);
                    ScrollMessagesToEnd();
                }
            }

            MessageTextBox.Clear();
            StatusTextBlock.Text = "消息已发送。";
        });

        SendButton.IsEnabled = _selectedGroup is not null || _selectedFriend is not null;
    }

    private async void AttachFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_apiClient is null || _currentUser is null)
        {
            StatusTextBlock.Text = "请先登录。";
            return;
        }

        if (_selectedGroup is null && _selectedFriend is null)
        {
            StatusTextBlock.Text = "请先选择好友或群聊。";
            return;
        }

        var dialog = new OpenFileDialog { Title = "选择要发送的文件" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var filePath = dialog.FileName;
        await RunWithStatusAsync("正在发送文件...", async () =>
        {
            AttachFileButton.IsEnabled = false;
            var connected = _signalRService?.State == HubConnectionState.Connected;

            if (_selectedGroup is not null)
            {
                var message = await _apiClient.SendGroupFileAsync(_selectedGroup.Id, filePath);
                if (!connected)
                {
                    AddGroupMessageToView(message);
                    ScrollMessagesToEnd();
                }
            }
            else
            {
                var message = await _apiClient.SendPrivateFileAsync(_selectedFriend!.Id, filePath);
                if (!connected)
                {
                    AddPrivateMessageToView(message);
                    ScrollMessagesToEnd();
                }
            }

            StatusTextBlock.Text = "文件已发送。";
        });

        AttachFileButton.IsEnabled = _selectedGroup is not null || _selectedFriend is not null;
    }

    private async void DownloadFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ChatMessageViewModel viewModel } || _apiClient is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(viewModel.DownloadUrl))
        {
            StatusTextBlock.Text = "该文件无法下载。";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "保存文件",
            FileName = viewModel.FileName
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var savePath = dialog.FileName;
        await RunWithStatusAsync("正在下载文件...", async () =>
        {
            await _apiClient.DownloadFileAsync(viewModel.DownloadUrl!, savePath);
            StatusTextBlock.Text = $"文件已保存到：{savePath}";
        });
    }

    private async Task RefreshFriendsAsync()
    {
        if (_apiClient is null)
        {
            return;
        }

        var selectedFriendId = _selectedFriend?.Id;
        var unreadCounts = _friends.ToDictionary(friend => friend.Id, friend => friend.UnreadCount);
        FriendsList.SelectedItem = null;
        _friends.Clear();

        foreach (var friend in await _apiClient.GetFriendsAsync())
        {
            var item = new FriendViewModel(friend);
            if (unreadCounts.TryGetValue(friend.Id, out var unreadCount))
            {
                item.UnreadCount = unreadCount;
            }

            _friends.Add(item);
        }

        if (selectedFriendId is not null)
        {
            foreach (var friend in _friends)
            {
                if (friend.Id == selectedFriendId.Value)
                {
                    FriendsList.SelectedItem = friend;
                    break;
                }
            }
        }
    }

    private async Task RefreshGroupsAsync()
    {
        if (_apiClient is null)
        {
            return;
        }

        var selectedGroupId = _selectedGroup?.Id;
        var unreadCounts = _groups.ToDictionary(group => group.Id, group => group.UnreadCount);
        GroupsList.SelectedItem = null;
        _groups.Clear();

        foreach (var group in await _apiClient.GetGroupsAsync())
        {
            var item = new GroupViewModel(group);
            if (unreadCounts.TryGetValue(group.Id, out var unreadCount))
            {
                item.UnreadCount = unreadCount;
            }

            _groups.Add(item);
        }

        if (selectedGroupId is null)
        {
            return;
        }

        var match = _groups.FirstOrDefault(group => group.Id == selectedGroupId.Value);
        if (match is not null)
        {
            GroupsList.SelectedItem = match;
        }
        else
        {
            // 当前所在群已退出或解散。
            _selectedGroup = null;
            GroupActionsPanel.Visibility = Visibility.Collapsed;
            _messages.Clear();
            SelectedFriendTextBlock.Text = "请选择好友或群聊";
            ConversationSubtitle.Text = "消息会实时同步到 Web 端和桌面端";
            SendButton.IsEnabled = false;
            AttachFileButton.IsEnabled = false;
        }
    }

    private async Task LoadAddableMembersAsync(GroupViewModel group)
    {
        if (_apiClient is null)
        {
            return;
        }

        _selectedGroupMembers = await _apiClient.GetGroupMembersAsync(group.Id);
        var memberIds = _selectedGroupMembers.Select(member => member.UserId).ToHashSet();
        var addable = _friends
            .Where(friend => !memberIds.Contains(friend.Id))
            .Select(friend => new FriendItem(friend.Id, friend.Username, friend.DisplayName))
            .ToList();

        AddMemberComboBox.DisplayMemberPath = "DisplayName";
        AddMemberComboBox.SelectedValuePath = "Id";
        AddMemberComboBox.ItemsSource = addable;
        if (addable.Count > 0)
        {
            AddMemberComboBox.SelectedIndex = 0;
        }
    }

    private async Task RefreshFriendRequestsAsync()
    {
        if (_apiClient is null)
        {
            return;
        }

        _friendRequests.Clear();
        foreach (var request in await _apiClient.GetFriendRequestsAsync())
        {
            _friendRequests.Add(request);
        }

        RequestsCountTextBlock.Text = $"{_friendRequests.Count} 条待处理";
        FriendRequestsEmptyTextBlock.Visibility = _friendRequests.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SignalRService_MessageReceived(MessageItem message)
    {
        Dispatcher.Invoke(() => HandleRealtimeMessage(message));
    }

    private void SignalRService_GroupMessageReceived(GroupMessageItem message)
    {
        Dispatcher.Invoke(() => HandleRealtimeGroupMessage(message));
    }

    private void HandleRealtimeMessage(MessageItem message)
    {
        if (_currentUser is null)
        {
            return;
        }

        var conversationUserId = message.FromUserId == _currentUser.UserId
            ? message.ToUserId
            : message.FromUserId;
        var friend = FindFriend(conversationUserId);

        if (friend is null)
        {
            StatusTextBlock.Text = "收到一条新消息，请刷新或重新登录后查看。";
            return;
        }

        if (_selectedFriend?.Id == conversationUserId)
        {
            AddPrivateMessageToView(message);
            ScrollMessagesToEnd();
            StatusTextBlock.Text = "收到实时消息。";
            friend.UnreadCount = 0;
            return;
        }

        friend.UnreadCount++;
        StatusTextBlock.Text = message.FromUserId == _currentUser.UserId
            ? $"你在其他端给 {friend.DisplayName} 发送了一条消息。"
            : $"收到来自 {friend.DisplayName} 的新消息。";
    }

    private void HandleRealtimeGroupMessage(GroupMessageItem message)
    {
        if (_currentUser is null)
        {
            return;
        }

        var group = FindGroup(message.GroupId);
        if (group is null)
        {
            StatusTextBlock.Text = "收到新的群消息，请刷新群聊列表。";
            return;
        }

        if (_selectedGroup?.Id == message.GroupId)
        {
            AddGroupMessageToView(message);
            ScrollMessagesToEnd();
            StatusTextBlock.Text = "收到群消息。";
            group.UnreadCount = 0;
            return;
        }

        group.UnreadCount++;
        StatusTextBlock.Text = $"群「{group.Name}」有新消息。";
    }

    private FriendViewModel? FindFriend(int friendId)
    {
        foreach (var friend in _friends)
        {
            if (friend.Id == friendId)
            {
                return friend;
            }
        }

        return null;
    }

    private GroupViewModel? FindGroup(int groupId)
    {
        foreach (var group in _groups)
        {
            if (group.Id == groupId)
            {
                return group;
            }
        }

        return null;
    }

    private void AddPrivateMessageToView(MessageItem message)
    {
        if (_currentUser is null)
        {
            return;
        }

        var isMine = message.FromUserId == _currentUser.UserId;
        var senderName = isMine
            ? _currentUser.DisplayName
            : _selectedFriend?.DisplayName ?? $"用户 {message.FromUserId}";

        AddMessageToView(isMine, senderName, message.Type, message.Content, message.FileName, message.FileSize, message.DownloadUrl, message.SentAt);
    }

    private void AddGroupMessageToView(GroupMessageItem message)
    {
        if (_currentUser is null)
        {
            return;
        }

        var isMine = message.FromUserId == _currentUser.UserId;
        var senderName = isMine ? _currentUser.DisplayName : message.FromDisplayName;

        AddMessageToView(isMine, senderName, message.Type, message.Content, message.FileName, message.FileSize, message.DownloadUrl, message.SentAt);
    }

    private void AddMessageToView(
        bool isMine,
        string senderName,
        int type,
        string content,
        string? fileName,
        long? fileSize,
        string? downloadUrl,
        DateTime sentAt)
    {
        if (type == 1)
        {
            _messages.Add(ChatMessageViewModel.CreateFile(
                senderName,
                string.IsNullOrWhiteSpace(fileName) ? content : fileName,
                fileSize,
                downloadUrl,
                sentAt,
                isMine));
        }
        else
        {
            _messages.Add(ChatMessageViewModel.CreateText(senderName, content, sentAt, isMine));
        }
    }

    private void ClearChatState()
    {
        _selectedFriend = null;
        _selectedGroup = null;
        _selectedGroupMembers = new List<GroupMemberApiModel>();
        FriendsList.SelectedItem = null;
        GroupsList.SelectedItem = null;
        _friends.Clear();
        _friendRequests.Clear();
        _groups.Clear();
        _messages.Clear();
        MessageTextBox.Clear();
        GroupActionsPanel.Visibility = Visibility.Collapsed;
        AddMemberComboBox.ItemsSource = null;
        SelectedFriendTextBlock.Text = "请选择好友或群聊";
        ConversationSubtitle.Text = "消息会实时同步到 Web 端和桌面端";
        CurrentUserTextBlock.Text = "未登录";
        ConnectionTextBlock.Text = "未连接";
        RequestsCountTextBlock.Text = "0 条待处理";
        FriendRequestsEmptyTextBlock.Visibility = Visibility.Visible;
        SendButton.IsEnabled = false;
        AttachFileButton.IsEnabled = false;
        StatusTextBlock.Text = "已进入聊天。";
    }

    private void ShowLoginView()
    {
        LoginView.Visibility = Visibility.Visible;
        ChatView.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            UsernameTextBox.Focus();
        }
        else
        {
            PasswordBox.Focus();
        }
    }

    private void ShowChatView()
    {
        LoginView.Visibility = Visibility.Collapsed;
        ChatView.Visibility = Visibility.Visible;
        MessageTextBox.Focus();
    }

    private void SetLoginStatus(string message, bool isError)
    {
        LoginStatusTextBlock.Text = message;
        LoginStatusTextBlock.Foreground = isError
            ? (Brush)FindResource("ErrorBrush")
            : (Brush)FindResource("MutedBrush");
    }

    private async Task LoadSavedSettingsAsync()
    {
        var settings = await _settingsService.LoadAsync();
        ServerUrlTextBox.Text = settings.ServerUrl;
        RememberUserCheckBox.IsChecked = settings.RememberUser;
        UsernameTextBox.Text = settings.RememberUser ? settings.Username : string.Empty;
        SetPassword(settings.RememberUser ? settings.Password : string.Empty);
        SetLoginStatus("请输入账号密码登录。", false);
    }

    private async Task SaveLoginSettingsAsync()
    {
        if (RememberUserCheckBox.IsChecked == true)
        {
            await _settingsService.SaveAsync(new DesktopSettings(
                ServerUrlTextBox.Text.Trim(),
                UsernameTextBox.Text.Trim(),
                PasswordBox.Password,
                true));
            return;
        }

        await _settingsService.SaveAsync(new DesktopSettings(
            ServerUrlTextBox.Text.Trim(),
            string.Empty,
            string.Empty,
            false));
    }

    private async Task ApplyLogoutLoginFieldsAsync()
    {
        if (RememberUserCheckBox.IsChecked == true)
        {
            await SaveLoginSettingsAsync();
            return;
        }

        UsernameTextBox.Clear();
        SetPassword(string.Empty);
        await SaveLoginSettingsAsync();
    }

    private void SetPassword(string password)
    {
        _isSyncingPassword = true;
        PasswordBox.Password = password;
        VisiblePasswordTextBox.Text = password;
        _isSyncingPassword = false;
    }

    private void ScrollMessagesToEnd()
    {
        if (_messages.Count > 0)
        {
            MessagesList.ScrollIntoView(_messages[^1]);
        }
    }

    private static string FormatDisplayName(string displayName, string username)
    {
        return string.Equals(displayName, username, StringComparison.OrdinalIgnoreCase)
            ? displayName
            : $"{displayName}（{username}）";
    }

    private async Task RunWithStatusAsync(string status, Func<Task> action)
    {
        try
        {
            StatusTextBlock.Text = status;
            await action();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = ex.Message;
            MessageBox.Show(ex.Message, "ChatSystem", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await DisconnectSignalRAsync();
        _apiClient?.Dispose();
    }

    private async Task DisconnectSignalRAsync()
    {
        if (_signalRService is null)
        {
            return;
        }

        _signalRService.MessageReceived -= SignalRService_MessageReceived;
        _signalRService.GroupMessageReceived -= SignalRService_GroupMessageReceived;
        await _signalRService.DisposeAsync();
        _signalRService = null;
        ConnectionTextBlock.Text = "未连接";
    }

    // 新的UI交互方法
    private void ResetLoginButton()
    {
        LoginButton.Content = "登录";
        LoginButton.Background = (Brush)FindResource("PrimaryBrush");
        LoginButton.FontWeight = FontWeights.SemiBold;
        LoginButton.IsEnabled = true;
    }

    private void ForgotPassword_Click(object sender, MouseButtonEventArgs e)
    {
        ShowToastNotification("忘记密码功能即将开放", "info");
    }

    private void RegisterLink_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = ServerUrlTextBox.Text + "/Account/Register",
            UseShellExecute = true
        });
    }

    // Toast通知实现
    private async void ShowToastNotification(string message, string type = "info")
    {
        var toast = new Border
        {
            Background = type == "error" ? (Brush)FindResource("ErrorBrush") :
                       type == "success" ? (Brush)FindResource("SuccessBrush") :
                       type == "warning" ? (Brush)FindResource("WarningBrush") :
                       (Brush)FindResource("PrimaryBrush"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20, 14, 20, 14),
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Width = 300,
            Child = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap
            }
        };

        // 计算位置 - 在窗口右上角显示
        var offset = new Thickness(0, 50, 50, 0);
        toast.Margin = offset;

        // 添加到主Grid
        var parent = this.Content as Grid;
        if (parent != null)
        {
            parent.Children.Add(toast);

            // 3秒后淡出并移除
            var fadeOutAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                BeginTime = TimeSpan.FromSeconds(2.7)
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                parent.Children.Remove(toast);
            };

            toast.BeginAnimation(Border.OpacityProperty, fadeOutAnimation);
        }
    }
}

public sealed class FriendViewModel : INotifyPropertyChanged
{
    private int _unreadCount;

    public FriendViewModel(FriendItem friend)
    {
        Id = friend.Id;
        Username = friend.Username;
        DisplayName = friend.DisplayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; }

    public string Username { get; }

    public string DisplayName { get; }

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (_unreadCount == value)
            {
                return;
            }

            _unreadCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UnreadBadge));
            OnPropertyChanged(nameof(UnreadVisibility));
        }
    }

    public string UnreadBadge => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

    public Visibility UnreadVisibility => UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class GroupViewModel : INotifyPropertyChanged
{
    private int _unreadCount;

    public GroupViewModel(ChatSystem.Desktop.Models.GroupItem group)
    {
        Id = group.Id;
        Name = group.Name;
        OwnerId = group.OwnerId;
        IsOwner = group.IsOwner;
        MemberCount = group.MemberCount;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; }

    public string Name { get; }

    public int OwnerId { get; }

    public bool IsOwner { get; }

    public int MemberCount { get; }

    public string SubtitleText => IsOwner ? $"{MemberCount} 人 · 群主" : $"{MemberCount} 人";

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (_unreadCount == value)
            {
                return;
            }

            _unreadCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UnreadBadge));
            OnPropertyChanged(nameof(UnreadVisibility));
        }
    }

    public string UnreadBadge => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

    public Visibility UnreadVisibility => UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ChatMessageViewModel
{
    private static readonly Brush MineBrush = new LinearGradientBrush(
        new GradientStopCollection
        {
            new GradientStop(Color.FromRgb(192, 132, 252), 0),   // #C084FC
            new GradientStop(Color.FromRgb(129, 140, 248), 1)   // #818CF8
        },
        new Point(0, 0),
        new Point(1, 1));
    private static readonly Brush MineTextBrush = Brushes.White;
    private static readonly Brush MineMetaBrush = new SolidColorBrush(Color.FromRgb(233, 217, 251)); // #E9D9FB
    private static readonly Brush BorderBrush = new SolidColorBrush(Color.FromRgb(168, 85, 247));

    private static readonly Brush OtherBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush OtherTextBrush = new SolidColorBrush(Color.FromRgb(59, 31, 110)); // #3B1F6E
    private static readonly Brush OtherMetaBrush = new SolidColorBrush(Color.FromRgb(139, 122, 171)); // #8B7AAB
    private static readonly Brush OtherBorderBrush = new SolidColorBrush(Color.FromRgb(233, 217, 251)); // #E9D9FB

    public string SenderName { get; private init; } = string.Empty;

    public string Content { get; private init; } = string.Empty;

    public string SentAtText { get; private init; } = string.Empty;

    public HorizontalAlignment BubbleAlignment { get; private init; }

    public Brush BubbleBrush { get; private init; } = OtherBrush;

    public Brush BubbleBorderBrush { get; private init; } = BorderBrush;

    public Brush TextBrush { get; private init; } = OtherTextBrush;

    public Brush MetaBrush { get; private init; } = OtherMetaBrush;

    public bool IsFile { get; private init; }

    public string FileName { get; private init; } = string.Empty;

    public string FileSizeText { get; private init; } = string.Empty;

    public string? DownloadUrl { get; private init; }

    public Visibility TextVisibility => IsFile ? Visibility.Collapsed : Visibility.Visible;

    public Visibility FileVisibility => IsFile ? Visibility.Visible : Visibility.Collapsed;

    public static ChatMessageViewModel CreateText(string senderName, string content, DateTime sentAt, bool isMine)
    {
        return new ChatMessageViewModel
        {
            SenderName = senderName,
            Content = content,
            SentAtText = sentAt.ToString("yyyy-MM-dd HH:mm:ss"),
            BubbleAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            BubbleBrush = isMine ? MineBrush : OtherBrush,
            BubbleBorderBrush = isMine ? MineBrush : BorderBrush,
            TextBrush = isMine ? MineTextBrush : OtherTextBrush,
            MetaBrush = isMine ? MineMetaBrush : OtherMetaBrush,
            IsFile = false
        };
    }

    public static ChatMessageViewModel CreateFile(
        string senderName,
        string fileName,
        long? fileSize,
        string? downloadUrl,
        DateTime sentAt,
        bool isMine)
    {
        return new ChatMessageViewModel
        {
            SenderName = senderName,
            Content = fileName,
            SentAtText = sentAt.ToString("yyyy-MM-dd HH:mm:ss"),
            BubbleAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            BubbleBrush = isMine ? MineBrush : OtherBrush,
            BubbleBorderBrush = isMine ? MineBrush : BorderBrush,
            TextBrush = isMine ? MineTextBrush : OtherTextBrush,
            MetaBrush = isMine ? MineMetaBrush : OtherMetaBrush,
            IsFile = true,
            FileName = fileName,
            FileSizeText = FormatSize(fileSize),
            DownloadUrl = downloadUrl
        };
    }

    private static string FormatSize(long? size)
    {
        if (size is null || size <= 0)
        {
            return string.Empty;
        }

        double value = size.Value;
        string[] units = { "B", "KB", "MB", "GB" };
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
