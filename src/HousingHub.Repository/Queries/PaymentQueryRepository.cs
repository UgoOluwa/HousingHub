using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PaymentQueryRepository : GenericQueryRepository<Payment>, IPaymentQueryRepository
{
    public PaymentQueryRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
