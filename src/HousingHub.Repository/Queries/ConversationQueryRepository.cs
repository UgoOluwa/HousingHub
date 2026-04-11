using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class ConversationQueryRepository : GenericQueryRepository<Conversation>, IConversationQueryRepository
{
    public ConversationQueryRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
