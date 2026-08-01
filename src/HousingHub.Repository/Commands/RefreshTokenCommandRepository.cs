using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class RefreshTokenCommandRepository : GenericCommandRepository<RefreshToken>, IRefreshTokenCommandRepository
{
    public RefreshTokenCommandRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
