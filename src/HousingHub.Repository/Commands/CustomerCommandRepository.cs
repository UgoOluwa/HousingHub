using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class CustomerCommandRepository : GenericCommandRepository<Customer>, ICustomerCommandRepository
{
    public CustomerCommandRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
