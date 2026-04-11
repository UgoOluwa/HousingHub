using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class ConversationCommandRepository : GenericCommandRepository<Conversation>, IConversationCommandRepository
{
    public ConversationCommandRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
