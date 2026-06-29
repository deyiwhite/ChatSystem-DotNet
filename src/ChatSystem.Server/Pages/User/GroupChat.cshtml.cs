using System.Security.Claims;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatSystem.Server.Pages.UserPages;

[Authorize(Roles = "User")]
public class GroupChatModel : PageModel
{
    private readonly GroupService _groupService;

    public GroupChatModel(GroupService groupService)
    {
        _groupService = groupService;
    }

    public int CurrentUserId { get; private set; }

    public List<GroupSummary> Groups { get; private set; } = new();

    public GroupSummary? SelectedGroup { get; private set; }

    public List<GroupMessageInfo> Messages { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? groupId)
    {
        CurrentUserId = GetCurrentUserId();
        Groups = await _groupService.GetGroupsForUserAsync(CurrentUserId);

        if (Groups.Count == 0)
        {
            return Page();
        }

        SelectedGroup = groupId is null
            ? Groups.FirstOrDefault()
            : Groups.SingleOrDefault(group => group.Id == groupId.Value);

        if (SelectedGroup is null)
        {
            // 选中的群不存在或当前用户不是成员，回到群组管理页。
            return RedirectToPage("/User/Groups");
        }

        Messages = await _groupService.GetMessagesAsync(SelectedGroup.Id, CurrentUserId);
        return Page();
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }
}
