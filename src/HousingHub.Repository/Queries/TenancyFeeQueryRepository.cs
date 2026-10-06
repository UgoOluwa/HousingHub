using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class TenancyFeeQueryRepository : GenericQueryRepository<TenancyFee>, ITenancyFeeQueryRepository
{
    public TenancyFeeQueryRepository(IDynamoDBContext context) : base(context) { }
}
