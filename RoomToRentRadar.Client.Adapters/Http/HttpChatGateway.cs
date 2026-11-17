using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Chat;
using RoomToRentRadar.Ports.Contracts.Responses.Chat;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>Chat endpoints arrive with T14.1; every operation is a task-id stub until then (PLAN §5.4).</summary>
public sealed class HttpChatGateway : IChatGateway
{
    public HttpChatGateway(HttpClient httpClient, ILogger<HttpChatGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public Task<ConversationDto> StartConversationAsync(string roomId, CancellationToken ct = default) =>
        Pending.For<ConversationDto>("T14.1", "startConversation");

    public Task<PagedResult<ConversationDto>> ListMyConversationsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default) =>
        Pending.For<PagedResult<ConversationDto>>("T14.1", "listMyConversations");

    public Task<ConversationDto> GetConversationAsync(string conversationId, CancellationToken ct = default) =>
        Pending.For<ConversationDto>("T14.1", "getConversation");

    public Task<MessagePage> GetConversationMessagesAsync(string conversationId, string? before = null, int limit = 30, CancellationToken ct = default) =>
        Pending.For<MessagePage>("T14.1", "getConversationMessages");

    public Task<MessageDto> SendMessageAsync(string conversationId, SendMessageRequest request, CancellationToken ct = default) =>
        Pending.For<MessageDto>("T14.1", "sendMessage");

    public Task MarkConversationReadAsync(string conversationId, CancellationToken ct = default) =>
        Pending.For("T14.1", "markConversationRead");
}