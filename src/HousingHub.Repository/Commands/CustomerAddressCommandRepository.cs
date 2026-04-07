using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class CustomerAddressCommandRepository : GenericCommandRepository<CustomerAddress>, ICustomerAddressCommandRepository
{
    public CustomerAddressCommandRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
