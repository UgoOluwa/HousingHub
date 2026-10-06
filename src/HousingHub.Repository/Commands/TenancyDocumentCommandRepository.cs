using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class TenancyDocumentCommandRepository : GenericCommandRepository<TenancyDocument>, ITenancyDocumentCommandRepository
{
    public TenancyDocumentCommandRepository(IDynamoDBContext context) : base(context) { }
}
