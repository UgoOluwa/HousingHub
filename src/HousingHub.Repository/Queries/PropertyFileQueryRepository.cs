using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyFileQueryRepository : GenericQueryRepository<PropertyFile>, IPropertyFileQueryRepository
{
    public PropertyFileQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
