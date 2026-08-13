using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class VerificationCaseCommandRepository : GenericCommandRepository<VerificationCase>, IVerificationCaseCommandRepository
{
    public VerificationCaseCommandRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
