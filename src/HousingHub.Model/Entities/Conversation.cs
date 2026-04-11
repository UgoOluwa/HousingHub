using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

[DynamoDBTable("Conversations")]
public class Conversation : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("ParticipantOneId-index")]
    public Guid ParticipantOneId { get; set; }

    [DynamoDBIgnore]
    public Customer ParticipantOne { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexHashKey("ParticipantTwoId-index")]
    public Guid ParticipantTwoId { get; set; }

    [DynamoDBIgnore]
    public Customer ParticipantTwo { get; set; } = null!;

    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }

    public Conversation() { }

    public Conversation(Guid participantOneId, Guid participantTwoId)
    {
        Id = Guid.NewGuid();
        ParticipantOneId = participantOneId;
        ParticipantTwoId = participantTwoId;
    }

    public bool HasParticipant(Guid userId) =>
        ParticipantOneId == userId || ParticipantTwoId == userId;

    public Guid GetOtherParticipantId(Guid userId) =>
        ParticipantOneId == userId ? ParticipantTwoId : ParticipantOneId;
}
