using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyReportCommandRepository : GenericCommandRepository<PropertyReport>, IPropertyReportCommandRepository
{
    public PropertyReportCommandRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
