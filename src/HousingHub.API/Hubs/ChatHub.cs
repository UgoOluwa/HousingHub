using HousingHub.Data.RepositoryInterfaces.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HousingHub.API.Hubs;

/// <summary>
/// Real-time chat transport.
/// </summary>
/// <remarks>
/// Conversation groups are named by conversation id, so joining a group is
/// equivalent to subscribing to that conversation's message stream. Membership
/// must therefore be verified server-side on every join — previously
/// <see cref="JoinConversation"/> added the caller to whatever group id they
/// passed, so any authenticated user who obtained or guessed a conversation id
/// received its messages in real time.
///
/// The REST endpoints in ChatQueryService/ChatCommandService already enforce
/// participation; this brings the hub in line with them.
/// </remarks>
[Authorize]
public class ChatHub : Hub
{
    private readonly IUnitOfWOrk _unitOfWork;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IUnitOfWOrk unitOfWork, ILogger<ChatHub> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>Key under which this connection's verified conversation ids are cached.</summary>
    private const string VerifiedKey = "verified-conversations";

    public async Task SendTypingIndicator(string conversationId)
    {
        var userId = Context.UserIdentifier;
        if (userId == null) return;

        // Typing indicators fire continuously while someone types. Only consult the
        // database if this connection hasn't already been verified for the
        // conversation, otherwise every keystroke becomes a read.
        if (!IsAlreadyVerified(conversationId)
            && !await CallerIsParticipantAsync(conversationId)) return;

        await Clients.Group(conversationId).SendAsync("UserTyping", conversationId, userId);
    }

    public async Task JoinConversation(string conversationId)
    {
        if (!await CallerIsParticipantAsync(conversationId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
    }

    /// <summary>
    /// Leaving is unguarded on purpose — removing yourself from a group you were
    /// never in is a no-op, and refusing it would serve no protective purpose.
    /// </summary>
    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId != null)
        {
            // Per-user group for direct notifications. The id comes from the
            // validated token via IUserIdProvider, not from the client.
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Verifies the connected user is one of the two participants in the given
    /// conversation. Returns false rather than throwing so a malicious or stale
    /// client is silently ignored instead of being told the conversation exists.
    /// </summary>
    private async Task<bool> CallerIsParticipantAsync(string conversationId)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            return false;

        if (!Guid.TryParse(conversationId, out var conversationGuid))
            return false;

        var conversation = await _unitOfWork.ConversationQueries.GetByIdAsync(conversationGuid);

        if (conversation is null || !conversation.HasParticipant(userId))
        {
            _logger.LogWarning(
                "Rejected chat hub access to conversation {ConversationId} for user {UserId}",
                conversationGuid, userId);
            return false;
        }

        VerifiedConversations.Add(conversationId);
        return true;
    }

    private bool IsAlreadyVerified(string conversationId) =>
        VerifiedConversations.Contains(conversationId);

    /// <summary>
    /// Conversation ids this connection has been verified against. Scoped to the
    /// connection, so it dies with the socket and cannot outlive a revoked session
    /// any longer than the connection itself.
    /// </summary>
    private HashSet<string> VerifiedConversations
    {
        get
        {
            if (Context.Items.TryGetValue(VerifiedKey, out var existing)
                && existing is HashSet<string> set)
            {
                return set;
            }

            var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Context.Items[VerifiedKey] = created;
            return created;
        }
    }
}
