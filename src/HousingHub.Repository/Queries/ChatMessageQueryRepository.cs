using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class ChatMessageQueryRepository : GenericQueryRepository<ChatMessage>, IChatMessageQueryRepository
{
    public ChatMessageQueryRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
