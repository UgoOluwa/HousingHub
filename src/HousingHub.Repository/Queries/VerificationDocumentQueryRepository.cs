using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class VerificationDocumentQueryRepository : GenericQueryRepository<VerificationDocument>, IVerificationDocumentQueryRepository
{
    public VerificationDocumentQueryRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
