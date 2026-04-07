using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyCommandRepository : GenericCommandRepository<Property>, IPropertyCommandRepository
{
    public PropertyCommandRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
