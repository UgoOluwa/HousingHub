using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class TenancyDocumentQueryRepository : GenericQueryRepository<TenancyDocument>, ITenancyDocumentQueryRepository
{
    public TenancyDocumentQueryRepository(IDynamoDBContext context) : base(context) { }
}
