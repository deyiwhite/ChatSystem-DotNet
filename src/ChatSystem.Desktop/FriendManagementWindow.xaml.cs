using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ChatSystem.Desktop.Models;
using ChatSystem.Desktop.Services;

namespace ChatSystem.Desktop
{

    public partial class FriendManagementWindow : Window
    {
        private readonly string _serverUrl;
        private readonly string _token;
        private readonly DesktopApiClient _apiClient;
        private readonly Action? _onFriendsChanged;

        public FriendManagementWindow(string serverUrl, string token, Action? onFriendsChanged = null)
        {
            InitializeComponent();
            _serverUrl = serverUrl;
            _token = token;
            _apiClient = new DesktopApiClient(serverUrl, token);
            _onFriendsChanged = onFriendsChanged;
            Loaded += async (s, e) => await LoadFriendsAsync();
            SearchTextBox.TextChanged += (s, e) => {
                var placeholder = SearchTextBox.Template.FindName("Placeholder", SearchTextBox) as System.Windows.Controls.TextBlock;
                if (placeholder != null)
                    placeholder.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                        ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        private async Task LoadFriendsAsync()
        {
            try
            {
                var friends = await _apiClient.GetFriendsAsync();

                if (friends?.Any() == true)
                {
                    FriendsList.ItemsSource = friends.Select(f => new FriendItem
                    {
                        Id = f.Id,
                        Username = f.Username,
                        DisplayName = f.DisplayName ?? f.Username
                    });

                    FriendsList.Visibility = Visibility.Visible;
                    FriendsEmptyPanel.Visibility = Visibility.Collapsed;
                    FriendsCountTextBlock.Text = $"{friends.Count} 个好友";
                }
                else
                {
                    FriendsList.Visibility = Visibility.Collapsed;
                    FriendsEmptyPanel.Visibility = Visibility.Visible;
                    FriendsCountTextBlock.Text = "0 个好友";
                }
            }
            catch (Exception ex)
            {
                ShowError($"加载好友列表失败: {ex.Message}");
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await SearchUsersAsync();
        }

        private async void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                await SearchUsersAsync();
            }
        }

        private async Task SearchUsersAsync()
        {
            var keyword = SearchTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
                SearchNoResultsTextBlock.Visibility = Visibility.Collapsed;
                SearchEmptyPanel.Visibility = Visibility.Visible;
                SearchResultsCountTextBlock.Text = "0 个用户";
                return;
            }

            try
            {
                // 调用搜索用户的API
                var users = await SearchUsersByKeywordAsync(keyword);

                if (users?.Any() == true)
                {
                    SearchResultsList.ItemsSource = users;
                    SearchResultsList.Visibility = Visibility.Visible;
                    SearchNoResultsTextBlock.Visibility = Visibility.Collapsed;
                    SearchEmptyPanel.Visibility = Visibility.Collapsed;
                    SearchResultsCountTextBlock.Text = $"{users.Count} 个用户";
                }
                else
                {
                    SearchResultsList.Visibility = Visibility.Collapsed;
                    SearchNoResultsTextBlock.Visibility = Visibility.Visible;
                    SearchEmptyPanel.Visibility = Visibility.Collapsed;
                    SearchResultsCountTextBlock.Text = "0 个用户";
                }
            }
            catch (Exception ex)
            {
                ShowError($"搜索用户失败: {ex.Message}");
            }
        }

        private async Task<List<SearchUserItem>> SearchUsersByKeywordAsync(string keyword)
        {
            try
            {
                var searchResults = await _apiClient.SearchUsersAsync(keyword);

                var result = searchResults?.Select(user => new ChatSystem.Desktop.Models.SearchUserItem(
                    user.Id,
                    user.Username,
                    user.DisplayName ?? user.Username,
                    user.CanRequest,
                    user.ActionText
                )).ToList() ?? new List<ChatSystem.Desktop.Models.SearchUserItem>();

                return result;
            }
            catch (Exception ex)
            {
                ShowError($"搜索用户API调用失败: {ex.Message}");
                return new List<SearchUserItem>();
            }
        }

        private async void SendRequestButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int userId)
            {
                try
                {
                    await _apiClient.SendFriendRequestAsync(userId);
                    ShowSuccess("好友申请已发送");

                    // 重新加载搜索结果，更新状态
                    await SearchUsersAsync();
                }
                catch (Exception ex)
                {
                    ShowError($"发送好友申请失败: {ex.Message}");
                }
            }
        }

        private async void DeleteFriendButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int friendId)
            {
                var result = MessageBox.Show(
                    "确定要删除这个好友吗？删除后需要重新发送好友申请才能再次成为好友。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _apiClient.DeleteFriendAsync(friendId);
                        ShowSuccess("已删除好友");
                        await LoadFriendsAsync();
                        _onFriendsChanged?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        ShowError($"删除好友失败: {ex.Message}");
                    }
                }
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadFriendsAsync();

            // 如果有搜索结果，重新搜索
            if (!string.IsNullOrWhiteSpace(SearchTextBox.Text.Trim()))
            {
                await SearchUsersAsync();
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccess(string message)
        {
            MessageBox.Show(message, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // 数据模型
    public class FriendItem
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public FriendItem()
        {
        }

        public FriendItem(int id, string username, string displayName)
        {
            Id = id;
            Username = username;
            DisplayName = displayName;
        }
    }



    // API 响应模型
    public class UserSearchResponse
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool CanRequest { get; set; }
        public string ActionText { get; set; } = string.Empty;
    }

}