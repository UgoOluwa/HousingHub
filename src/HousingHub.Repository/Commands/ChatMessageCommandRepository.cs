using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class ChatMessageCommandRepository : GenericCommandRepository<ChatMessage>, IChatMessageCommandRepository
{
    public ChatMessageCommandRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
