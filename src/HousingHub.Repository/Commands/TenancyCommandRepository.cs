using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class TenancyCommandRepository : GenericCommandRepository<Tenancy>, ITenancyCommandRepository
{
    public TenancyCommandRepository(IDynamoDBContext context) : base(context) { }
}
