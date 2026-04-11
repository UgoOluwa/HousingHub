using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

[DynamoDBTable("ChatMessages")]
public class ChatMessage : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("ConversationId-index")]
    public Guid ConversationId { get; set; }

    [DynamoDBIgnore]
    public Conversation Conversation { get; set; } = null!;

    public Guid SenderId { get; set; }

    [DynamoDBIgnore]
    public Customer Sender { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool IsRead { get; set; } = false;

    public ChatMessage() { }

    public ChatMessage(Guid conversationId, Guid senderId, string content)
    {
        Id = Guid.NewGuid();
        ConversationId = conversationId;
        SenderId = senderId;
        Content = content;
    }
}
