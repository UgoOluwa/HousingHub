using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyReportQueryRepository : GenericQueryRepository<PropertyReport>, IPropertyReportQueryRepository
{
    public PropertyReportQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
