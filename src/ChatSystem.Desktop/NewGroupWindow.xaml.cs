using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using ChatSystem.Desktop.Models;

namespace ChatSystem.Desktop;

public partial class NewGroupWindow : Window
{
    private readonly List<FriendCheckItem> _items;

    public NewGroupWindow(IEnumerable<FriendItem> friends)
    {
        InitializeComponent();
        _items = friends
            .Select(friend => new FriendCheckItem(friend.Id, friend.Username, friend.DisplayName))
            .ToList();
        FriendsCheckList.ItemsSource = _items;
        GroupNameTextBox.Focus();
    }

    public string GroupName { get; private set; } = string.Empty;

    public List<int> SelectedFriendIds { get; private set; } = new();

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var name = GroupNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("请输入群名称。", "新建群聊", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        GroupName = name;
        SelectedFriendIds = _items.Where(item => item.IsSelected).Select(item => item.Id).ToList();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public sealed class FriendCheckItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public FriendCheckItem(int id, string username, string displayName)
    {
        Id = id;
        Username = username;
        DisplayName = displayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; }

    public string Username { get; }

    public string DisplayName { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
