using System.Security.Claims;
using ChatSystem.Server.Authentication;
using ChatSystem.Server.Hubs;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChatSystem.Server.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize(
    AuthenticationSchemes = DesktopTokenAuthenticationDefaults.AuthenticationScheme,
    Roles = "User")]
public class GroupsController : ControllerBase
{
    private readonly GroupService _groupService;
    private readonly IHubContext<ChatHub> _hubContext;

    public GroupsController(GroupService groupService, IHubContext<ChatHub> hubContext)
    {
        _groupService = groupService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<GroupSummary>>> GetGroups()
    {
        return await _groupService.GetGroupsForUserAsync(GetCurrentUserId());
    }

    [HttpPost]
    public async Task<ActionResult<GroupSummary>> CreateGroup(CreateGroupRequest request)
    {
        try
        {
            var group = await _groupService.CreateGroupAsync(
                GetCurrentUserId(),
                request.Name,
                request.MemberIds ?? new List<int>());
            return group;
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpGet("{groupId:int}/members")]
    public async Task<ActionResult<List<GroupMemberInfo>>> GetMembers(int groupId)
    {
        try
        {
            return await _groupService.GetMembersAsync(groupId, GetCurrentUserId());
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPost("{groupId:int}/members")]
    public async Task<IActionResult> AddMember(int groupId, AddMemberRequest request)
    {
        try
        {
            await _groupService.AddMemberAsync(groupId, GetCurrentUserId(), request.UserId);
            return NoContent();
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPost("{groupId:int}/members/{userId:int}/remove")]
    public async Task<IActionResult> RemoveMember(int groupId, int userId)
    {
        try
        {
            await _groupService.RemoveMemberAsync(groupId, GetCurrentUserId(), userId);
            return NoContent();
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPost("{groupId:int}/leave")]
    public async Task<IActionResult> Leave(int groupId)
    {
        try
        {
            await _groupService.LeaveGroupAsync(groupId, GetCurrentUserId());
            return NoContent();
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpDelete("{groupId:int}")]
    public async Task<IActionResult> Disband(int groupId)
    {
        try
        {
            await _groupService.DisbandGroupAsync(groupId, GetCurrentUserId());
            return NoContent();
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpGet("{groupId:int}/messages")]
    public async Task<ActionResult<List<GroupMessageInfo>>> GetMessages(int groupId)
    {
        try
        {
            return await _groupService.GetMessagesAsync(groupId, GetCurrentUserId());
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<GroupMessageInfo>>> GetGroupHistory(
    [FromQuery] int? groupId,
    [FromQuery] string? keyword,
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate)
    {
        try
        {
            var userId = GetCurrentUserId();
            var messages = await _groupService.GetGroupHistoryAsync(userId, groupId, keyword, startDate, endDate);
            return messages;
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPost("{groupId:int}/messages")]
    public async Task<ActionResult<GroupMessageInfo>> SendMessage(int groupId, SendGroupMessageRequest request)
    {
        try
        {
            var (message, memberIds) = await _groupService.AddTextMessageAsync(
                groupId,
                GetCurrentUserId(),
                GetCurrentDisplayName(),
                request.Content);

            var payload = new GroupMessagePayload(
                message.Id,
                message.GroupId,
                message.FromUserId,
                message.FromDisplayName,
                message.Content,
                message.Type,
                message.FileName,
                message.FileSize,
                message.DownloadUrl,
                message.SentAt);

            await ChatHub.BroadcastGroupMessageAsync(_hubContext.Clients, memberIds, payload);

            return message;
        }
        catch (GroupException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    private string GetCurrentDisplayName()
    {
        return User.FindFirstValue("DisplayName")
               ?? User.FindFirstValue(ClaimTypes.Name)
               ?? "用户";
    }
}

public sealed record CreateGroupRequest(string Name, List<int>? MemberIds);

public sealed record AddMemberRequest(int UserId);

public sealed record SendGroupMessageRequest(string Content);
