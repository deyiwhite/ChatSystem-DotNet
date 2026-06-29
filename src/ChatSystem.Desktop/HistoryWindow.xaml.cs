using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using ChatSystem.Desktop.Services;
using ChatSystem.Desktop.Models;
using GroupItem = ChatSystem.Desktop.Models.GroupItem;

namespace ChatSystem.Desktop
{
    public partial class HistoryWindow : Window
    {
        private readonly string _serverUrl;
        private readonly string _token;
        private readonly DesktopApiClient _apiClient;
        private List<FriendItem> _friends = new();
        private List<GroupItem> _groups = new();
        private bool _isGroupTab = false;

        public HistoryWindow(string serverUrl, string token)
        {
            InitializeComponent();
            _serverUrl = serverUrl;
            _token = token;
            _apiClient = new DesktopApiClient(serverUrl, token);
            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // 加载好友列表
                _friends = await _apiClient.GetFriendsAsync();
                var friendItems = new List<ComboBoxItem>
                {
                    new ComboBoxItem { Content = "全部好友", Tag = 0 }
                };
                foreach (var f in _friends)
                    friendItems.Add(new ComboBoxItem { Content = $"{f.DisplayName}（{f.Username}）", Tag = f.Id });
                FriendComboBox.ItemsSource = friendItems;
                FriendComboBox.SelectedIndex = 0;

                // 加载群组列表
                _groups = await _apiClient.GetGroupsAsync();
                var groupItems = new List<ComboBoxItem>
                {
                    new ComboBoxItem { Content = "全部群组", Tag = 0 }
                };
                foreach (var g in _groups)
                    groupItems.Add(new ComboBoxItem { Content = g.Name, Tag = g.Id });
                GroupComboBox.ItemsSource = groupItems;
                GroupComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowError($"初始化失败: {ex.Message}");
            }
        }

        // ── Tab 切换 ──────────────────────────────────────────────

        private void PrivateTabButton_Click(object sender, RoutedEventArgs e)
        {
            _isGroupTab = false;
            PrivateTabButton.Style = (Style)FindResource("TabButtonActiveStyle");
            GroupTabButton.Style = (Style)FindResource("TabButtonStyle");
            PrivateFilterPanel.Visibility = Visibility.Visible;
            GroupFilterPanel.Visibility = Visibility.Collapsed;
            MessagesList.Visibility = Visibility.Collapsed;
            GroupMessagesList.Visibility = Visibility.Collapsed;
            MessagesEmptyPanel.Visibility = Visibility.Visible;
            NoResultsTextBlock.Visibility = Visibility.Collapsed;
            MessagesCountTextBlock.Text = "0 条消息";
        }

        private void GroupTabButton_Click(object sender, RoutedEventArgs e)
        {
            _isGroupTab = true;
            GroupTabButton.Style = (Style)FindResource("TabButtonActiveStyle");
            PrivateTabButton.Style = (Style)FindResource("TabButtonStyle");
            PrivateFilterPanel.Visibility = Visibility.Collapsed;
            GroupFilterPanel.Visibility = Visibility.Visible;
            MessagesList.Visibility = Visibility.Collapsed;
            GroupMessagesList.Visibility = Visibility.Collapsed;
            MessagesEmptyPanel.Visibility = Visibility.Visible;
            NoResultsTextBlock.Visibility = Visibility.Collapsed;
            MessagesCountTextBlock.Text = "0 条消息";
        }

        // ── 私聊筛选 ──────────────────────────────────────────────

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
            => await SearchPrivateMessagesAsync();

        private async void KeywordTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                await SearchPrivateMessagesAsync();
        }

        private async void FriendComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FriendComboBox.SelectedItem != null && IsLoaded && !_isGroupTab)
                await SearchPrivateMessagesAsync();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            KeywordTextBox.Text = string.Empty;
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            FriendComboBox.SelectedIndex = 0;
        }

        private async Task SearchPrivateMessagesAsync()
        {
            try
            {
                var friendId = GetComboBoxTagInt(FriendComboBox);
                var keyword = KeywordTextBox.Text.Trim();
                var startDate = StartDatePicker.SelectedDate;
                var endDate = EndDatePicker.SelectedDate;

                var raw = await _apiClient.GetHistoryMessagesAsync(
                    friendId == 0 ? null : friendId,
                    string.IsNullOrWhiteSpace(keyword) ? null : keyword,
                    startDate, endDate);

                var messages = raw.Select(m => new HistoryMessageViewModel
                {
                    Id = m.Id,
                    FromName = m.FromName,
                    ToName = $"发送给 {m.ToName}",
                    FromInitial = GetInitial(m.FromName),
                    Content = m.Content,
                    SentAtFormatted = m.SentAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsTextMessage = m.Type == 0,
                    IsFileMessage = m.Type == 1,
                    FileName = m.FileName,
                    FileSizeText = FormatFileSize(m.FileSize),
                    DownloadUrl = m.DownloadUrl,
                    IsMine = m.IsMine
                }).OrderByDescending(m => m.SentAtFormatted).ToList();

                ShowPrivateResults(messages);
            }
            catch (Exception ex) { ShowError($"搜索失败: {ex.Message}"); }
        }

        private void ShowPrivateResults(List<HistoryMessageViewModel> messages)
        {
            GroupMessagesList.Visibility = Visibility.Collapsed;
            if (messages.Any())
            {
                MessagesList.ItemsSource = messages;
                MessagesList.Visibility = Visibility.Visible;
                MessagesEmptyPanel.Visibility = Visibility.Collapsed;
                NoResultsTextBlock.Visibility = Visibility.Collapsed;
                MessagesCountTextBlock.Text = $"{messages.Count} 条消息";
            }
            else
            {
                MessagesList.Visibility = Visibility.Collapsed;
                MessagesEmptyPanel.Visibility = Visibility.Collapsed;
                NoResultsTextBlock.Visibility = Visibility.Visible;
                MessagesCountTextBlock.Text = "0 条消息";
            }
        }

        // ── 群聊筛选 ──────────────────────────────────────────────

        private async void GroupSearchButton_Click(object sender, RoutedEventArgs e)
            => await SearchGroupMessagesAsync();

        private async void GroupKeywordTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                await SearchGroupMessagesAsync();
        }

        private async void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupComboBox.SelectedItem != null && IsLoaded && _isGroupTab)
                await SearchGroupMessagesAsync();
        }

        private void GroupResetButton_Click(object sender, RoutedEventArgs e)
        {
            GroupKeywordTextBox.Text = string.Empty;
            GroupStartDatePicker.SelectedDate = null;
            GroupEndDatePicker.SelectedDate = null;
            GroupComboBox.SelectedIndex = 0;
        }

        private async Task SearchGroupMessagesAsync()
        {
            try
            {
                var groupId = GetComboBoxTagInt(GroupComboBox);
                var keyword = GroupKeywordTextBox.Text.Trim();
                var startDate = GroupStartDatePicker.SelectedDate;
                var endDate = GroupEndDatePicker.SelectedDate;

                var raw = await _apiClient.GetGroupHistoryMessagesAsync(
                    groupId == 0 ? null : groupId,
                    string.IsNullOrWhiteSpace(keyword) ? null : keyword,
                    startDate, endDate);

                // 找群名
                var groupNameMap = _groups.ToDictionary(g => g.Id, g => g.Name);

                var messages = raw.Select(m => new GroupHistoryMessageViewModel
                {
                    FromDisplayName = m.FromDisplayName,
                    GroupName = groupNameMap.TryGetValue(m.GroupId, out var gn) ? $"「{gn}」" : $"群组{m.GroupId}",
                    FromInitial = GetInitial(m.FromDisplayName),
                    Content = m.Content,
                    SentAtFormatted = m.SentAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsTextMessage = m.Type == 0,
                    IsFileMessage = m.Type == 1,
                    FileName = m.FileName,
                    FileSizeText = FormatFileSize(m.FileSize),
                    DownloadUrl = m.DownloadUrl
                }).OrderByDescending(m => m.SentAtFormatted).ToList();

                ShowGroupResults(messages);
            }
            catch (Exception ex) { ShowError($"搜索失败: {ex.Message}"); }
        }

        private void ShowGroupResults(List<GroupHistoryMessageViewModel> messages)
        {
            MessagesList.Visibility = Visibility.Collapsed;
            if (messages.Any())
            {
                GroupMessagesList.ItemsSource = messages;
                GroupMessagesList.Visibility = Visibility.Visible;
                MessagesEmptyPanel.Visibility = Visibility.Collapsed;
                NoResultsTextBlock.Visibility = Visibility.Collapsed;
                MessagesCountTextBlock.Text = $"{messages.Count} 条消息";
            }
            else
            {
                GroupMessagesList.Visibility = Visibility.Collapsed;
                MessagesEmptyPanel.Visibility = Visibility.Collapsed;
                NoResultsTextBlock.Visibility = Visibility.Visible;
                MessagesCountTextBlock.Text = "0 条消息";
            }
        }

        // ── 删除 / 下载 ───────────────────────────────────────────

        private async void DeleteMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: int messageId }) return;
            var result = MessageBox.Show("确定要删除这条消息记录吗？删除后仅对自己不可见。",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            try
            {
                await _apiClient.DeleteHistoryMessageAsync(messageId);
                ShowSuccess("消息记录已删除");
                await SearchPrivateMessagesAsync();
            }
            catch (Exception ex) { ShowError($"删除失败: {ex.Message}"); }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string downloadUrl }) return;
            try
            {
                var saveDialog = new SaveFileDialog { Filter = "所有文件 (*.*)|*.*", Title = "保存文件" };
                if (saveDialog.ShowDialog() == true)
                {
                    await _apiClient.DownloadFileAsync(downloadUrl, saveDialog.FileName);
                    ShowSuccess("文件下载成功");
                }
            }
            catch (Exception ex) { ShowError($"下载失败: {ex.Message}"); }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isGroupTab) await SearchGroupMessagesAsync();
            else await SearchPrivateMessagesAsync();
        }

        // ── 工具方法 ──────────────────────────────────────────────

        private static int? GetComboBoxTagInt(ComboBox cb)
            => cb.SelectedItem is ComboBoxItem { Tag: int v } ? v : (int?)null;

        private static string GetInitial(string name)
            => string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper();

        private static string FormatFileSize(long? size)
        {
            if (size is null || size <= 0) return string.Empty;
            double v = size.Value;
            string[] units = { "B", "KB", "MB", "GB" };
            var u = 0;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            return $"{v:0.#} {units[u]}";
        }

        private void ShowError(string message)
            => MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);

        private void ShowSuccess(string message)
            => MessageBox.Show(message, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── ViewModel ─────────────────────────────────────────────────

    public class HistoryMessageViewModel
    {
        public int Id { get; set; }
        public string FromName { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public string FromInitial { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SentAtFormatted { get; set; } = string.Empty;
        public bool IsTextMessage { get; set; }
        public bool IsFileMessage { get; set; }
        public string? FileName { get; set; }
        public string? FileSizeText { get; set; }
        public string? DownloadUrl { get; set; }
        public bool IsMine { get; set; }
    }

    public class GroupHistoryMessageViewModel
    {
        public string FromDisplayName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string FromInitial { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SentAtFormatted { get; set; } = string.Empty;
        public bool IsTextMessage { get; set; }
        public bool IsFileMessage { get; set; }
        public string? FileName { get; set; }
        public string? FileSizeText { get; set; }
        public string? DownloadUrl { get; set; }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility.Visible;
    }

    // 旧的 HistoryMessageItem 保留，避免其他地方引用报错
    public class HistoryMessageItem
    {
        public int Id { get; set; }
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public string FromName { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public string FromInitial { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string SentAtFormatted { get; set; } = string.Empty;
        public int Type { get; set; }
        public bool IsTextMessage { get; set; }
        public bool IsFileMessage { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? FileSizeText { get; set; }
        public string? DownloadUrl { get; set; }
        public bool IsMine { get; set; }
    }
}