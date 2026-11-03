using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PayoutAccountCommandRepository : GenericCommandRepository<PayoutAccount>, IPayoutAccountCommandRepository
{
    public PayoutAccountCommandRepository(IDynamoDBContext context) : base(context) { }
}
