using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class CustomerQueryRepository : GenericQueryRepository<Customer>, ICustomerQueryRepository
{
    public CustomerQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
