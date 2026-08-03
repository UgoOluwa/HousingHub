using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyAlertPreferenceCommandRepository : GenericCommandRepository<PropertyAlertPreference>, IPropertyAlertPreferenceCommandRepository
{
    public PropertyAlertPreferenceCommandRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
