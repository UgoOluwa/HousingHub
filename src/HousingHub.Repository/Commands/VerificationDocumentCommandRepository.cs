using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class VerificationDocumentCommandRepository : GenericCommandRepository<VerificationDocument>, IVerificationDocumentCommandRepository
{
    public VerificationDocumentCommandRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
