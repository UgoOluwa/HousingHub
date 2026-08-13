using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class VerificationCaseQueryRepository : GenericQueryRepository<VerificationCase>, IVerificationCaseQueryRepository
{
    public VerificationCaseQueryRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
