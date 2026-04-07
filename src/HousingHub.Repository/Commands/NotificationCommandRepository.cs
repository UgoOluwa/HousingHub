using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class NotificationCommandRepository : GenericCommandRepository<Notification>, INotificationCommandRepository
{
    public NotificationCommandRepository(IDynamoDBContext context)
        : base(context)
    {
    }
}
