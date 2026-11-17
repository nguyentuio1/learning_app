using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartDroneDelivery.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AssistantMessageRequest
{
    [JsonRequired] public Guid? SessionId { get; set; }
    [JsonRequired] public Guid? OrderId { get; set; }
    [JsonRequired] public string? Message { get; set; }
}

public class AssistantPageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 50)] public int PageSize { get; set; } = 20;
}

public record AssistantReplyDto(string? Id, string SenderType, string MessageText, DateTime CreatedAt);
public record AssistantResponseDto(string Outcome, Guid? SessionId, string? UserMessageId, AssistantReplyDto Reply);
public record AssistantSessionDto(Guid Id, string? SessionTitle, DateTime CreatedAt);
public record AssistantSessionsDto(AssistantSessionDto[] Items, int Page, int PageSize, int TotalCount);
public record AssistantMessagesDto(Guid SessionId, AssistantReplyDto[] Items, int Page, int PageSize, int TotalCount);
