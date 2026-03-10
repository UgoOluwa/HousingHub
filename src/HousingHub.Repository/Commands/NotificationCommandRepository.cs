using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class NotificationCommandRepository : GenericCommandRepository<Notification>, INotificationCommandRepository
{
    public NotificationCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }
}
