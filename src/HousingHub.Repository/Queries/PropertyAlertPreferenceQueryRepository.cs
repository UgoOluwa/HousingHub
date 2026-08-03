using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyAlertPreferenceQueryRepository : GenericQueryRepository<PropertyAlertPreference>, IPropertyAlertPreferenceQueryRepository
{
    public PropertyAlertPreferenceQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
