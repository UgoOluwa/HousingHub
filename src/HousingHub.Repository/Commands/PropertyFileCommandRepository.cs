using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyFileCommandRepository : GenericCommandRepository<PropertyFile>, IPropertyFileCommandRepository
{
    public PropertyFileCommandRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
