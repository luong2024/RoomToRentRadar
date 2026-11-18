using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Chat;
using RoomToRentRadar.Ports.Contracts.Responses.Chat;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>In-app chat (Chat tag of <c>docs/api/openapi.yaml</c>, US14, BR-CHT-01..05).</summary>
public interface IChatGateway
{
    [ApiOperation("startConversation")]
    Task<ConversationDto> StartConversationAsync(string roomId, CancellationToken ct = default);

    [ApiOperation("listMyConversations")]
    Task<PagedResult<ConversationDto>> ListMyConversationsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);

    [ApiOperation("getConversation")]
    Task<ConversationDto> GetConversationAsync(string conversationId, CancellationToken ct = default);

    [ApiOperation("getConversationMessages")]
    Task<MessagePage> GetConversationMessagesAsync(string conversationId, string? before = null, int limit = 30, CancellationToken ct = default);

    [ApiOperation("sendMessage")]
    Task<MessageDto> SendMessageAsync(string conversationId, SendMessageRequest request, CancellationToken ct = default);

    [ApiOperation("markConversationRead")]
    Task MarkConversationReadAsync(string conversationId, CancellationToken ct = default);
}