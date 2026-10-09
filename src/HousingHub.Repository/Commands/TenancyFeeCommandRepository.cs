using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class TenancyFeeCommandRepository : GenericCommandRepository<TenancyFee>, ITenancyFeeCommandRepository
{
    public TenancyFeeCommandRepository(IDynamoDBContext context) : base(context) { }
}
