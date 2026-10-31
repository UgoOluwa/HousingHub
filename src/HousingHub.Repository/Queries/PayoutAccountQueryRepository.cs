using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PayoutAccountQueryRepository : GenericQueryRepository<PayoutAccount>, IPayoutAccountQueryRepository
{
    public PayoutAccountQueryRepository(IDynamoDBContext context) : base(context) { }
}
