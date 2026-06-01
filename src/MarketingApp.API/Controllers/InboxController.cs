using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces.AI;
using MarketingApp.Application.Interfaces.Inbox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/inbox")]
[Authorize]
public class InboxController : ControllerBase
{
    private readonly IInboxService _service;
    private readonly IAiReplyService _aiReply;
    private readonly IAiChatService _aiChat;

    public InboxController(IInboxService service, IAiReplyService aiReply, IAiChatService aiChat)
    { _service = service; _aiReply = aiReply; _aiChat = aiChat; }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool viewAll = false,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] string? category = null,
        [FromQuery] Guid? smtpGroupId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // BUG-003 defensive clamp.
        Helpers.PagingHelper.Clamp(ref pageNumber, ref pageSize);
        var result = await _service.GetAllAsync(
            CurrentUserId, IsAdmin, viewAll, pageNumber, pageSize,
            unreadOnly, category, smtpGroupId, search, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(id, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<InboxMessageDetailDto>.Ok(dto));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var dto = await _service.GetUnreadCountAsync(CurrentUserId, ct);
        return Ok(ApiResponse<InboxUnreadCountDto>.Ok(dto));
    }

    // === Day 8: conversation (thread) views ===
    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool viewAll = false,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // BUG-003 defensive clamp.
        Helpers.PagingHelper.Clamp(ref pageNumber, ref pageSize);
        var result = await _service.GetThreadsAsync(
            CurrentUserId, IsAdmin, viewAll, pageNumber, pageSize, unreadOnly, category, search, ct);
        return Ok(result);
    }

    [HttpGet("threads/{threadId:guid}")]
    public async Task<IActionResult> GetThread(Guid threadId, CancellationToken ct)
    {
        var dto = await _service.GetThreadByIdAsync(threadId, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<InboxThreadDetailDto>.Ok(dto));
    }

    [HttpDelete("threads/{threadId:guid}")]
    public async Task<IActionResult> DeleteThread(Guid threadId, CancellationToken ct)
    {
        await _service.DeleteThreadAsync(threadId, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Conversation deleted."));
    }

    /// <summary>Wipe the user's inbox. Pass resetImapCursor=true to re-fetch the same emails on next poll (re-test from scratch).</summary>
    [HttpPost("clear")]
    public async Task<IActionResult> ClearAll([FromQuery] bool resetImapCursor = false, CancellationToken ct = default)
    {
        var deleted = await _service.ClearAllAsync(CurrentUserId, IsAdmin, resetImapCursor, ct);
        return Ok(ApiResponse<object>.Ok(new { deleted, resetImapCursor }, $"Cleared {deleted} message(s)."));
    }

    // === Day 9: Ask-AI chat agent (per thread) ===
    [HttpGet("threads/{threadId:guid}/chat")]
    public async Task<IActionResult> GetChat(Guid threadId, CancellationToken ct)
    {
        var history = await _aiChat.GetHistoryAsync(threadId, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<IReadOnlyList<AiChatTurnDto>>.Ok(history));
    }

    [HttpPost("threads/{threadId:guid}/chat")]
    public async Task<IActionResult> AskChat(Guid threadId, [FromBody] AskAiChatDto dto, CancellationToken ct)
    {
        var answer = await _aiChat.AskAsync(threadId, dto.Question ?? "", CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<AiChatTurnDto>.Ok(answer));
    }

    [HttpDelete("threads/{threadId:guid}/chat")]
    public async Task<IActionResult> ClearChat(Guid threadId, CancellationToken ct)
    {
        await _aiChat.ClearHistoryAsync(threadId, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Chat cleared."));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _service.MarkReadAsync(id, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Marked as read."));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _service.MarkAllReadAsync(CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(null!, "All messages marked as read."));
    }

    [HttpPut("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _service.ArchiveAsync(id, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Archived."));
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveDraftDto dto, CancellationToken ct)
    {
        await _service.SaveDraftAsync(id, dto.Html ?? "", CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Draft saved."));
    }

    [HttpPost("{id:guid}/regenerate-ai")]
    public async Task<IActionResult> RegenerateAi(Guid id, CancellationToken ct)
    {
        // Verify access first (throws if user doesn't own the message).
        await _service.GetByIdAsync(id, CurrentUserId, IsAdmin, ct);
        await _aiReply.ProcessAsync(id, ct);
        var refreshed = await _service.GetByIdAsync(id, CurrentUserId, IsAdmin, ct);

        // If AI couldn't generate (provider disabled, key missing, API call failed) surface the error
        // to the client so the user sees what went wrong instead of a silent empty editor.
        if (string.IsNullOrEmpty(refreshed.AiSuggestedReply) && !string.IsNullOrEmpty(refreshed.AiGenerationError))
        {
            return BadRequest(ApiResponse<InboxMessageDetailDto>.Fail(refreshed.AiGenerationError));
        }

        return Ok(ApiResponse<InboxMessageDetailDto>.Ok(refreshed, "AI suggestion regenerated."));
    }

    [HttpPost("{id:guid}/send-reply")]
    public async Task<IActionResult> SendReply(Guid id, [FromBody] SendReplyDto dto, CancellationToken ct)
    {
        await _service.SendReplyAsync(id, dto, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Reply sent."));
    }
}
