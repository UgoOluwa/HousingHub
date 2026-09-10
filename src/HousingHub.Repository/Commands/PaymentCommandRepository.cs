using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PaymentCommandRepository : GenericCommandRepository<Payment>, IPaymentCommandRepository
{
    public PaymentCommandRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
