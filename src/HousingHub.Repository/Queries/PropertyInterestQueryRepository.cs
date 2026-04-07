using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyInspectionQueryRepository : GenericQueryRepository<PropertyInspection>, IPropertyInspectionQueryRepository
{
    public PropertyInspectionQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
