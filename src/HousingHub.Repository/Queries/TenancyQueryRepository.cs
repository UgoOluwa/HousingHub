using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class TenancyQueryRepository : GenericQueryRepository<Tenancy>, ITenancyQueryRepository
{
    public TenancyQueryRepository(IDynamoDBContext context) : base(context) { }
}
