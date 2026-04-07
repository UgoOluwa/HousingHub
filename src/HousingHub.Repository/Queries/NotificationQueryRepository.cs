using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class NotificationQueryRepository : GenericQueryRepository<Notification>, INotificationQueryRepository
{
    public NotificationQueryRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
