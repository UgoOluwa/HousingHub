using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class CustomerAddressQueryRepository : GenericQueryRepository<CustomerAddress>, ICustomerAddressQueryRepository
{
    public CustomerAddressQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
