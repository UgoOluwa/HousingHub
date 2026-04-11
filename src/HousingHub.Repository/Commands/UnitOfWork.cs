using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Data.RepositoryInterfaces.Queries;

namespace HousingHub.Repository.Commands;

public class UnitOfWork : IUnitOfWOrk
{
    public ICustomerAddressCommandRepository CustomerAddressCommands { get; }
    public ICustomerAddressQueryRepository CustomerAddressQueries { get; }
    public ICustomerCommandRepository CustomerCommands { get; }
    public ICustomerQueryRepository CustomerQueries { get; }
    public IPropertyInspectionCommandRepository PropertyInspectionCommands { get; }
    public IPropertyInspectionQueryRepository PropertyInspectionQueries { get; }
    public IPropertyAddressCommandRepository PropertyAddressCommands { get; }
    public IPropertyAddressQueryRepository PropertyAddressQueries { get; }
    public IPropertyFileCommandRepository PropertyFileCommands { get; }
    public IPropertyFileQueryRepository PropertyFileQueries { get; }
    public IPropertyCommandRepository PropertyCommands { get; }
    public IPropertyQueryRepository PropertyQueries { get; }
    public INotificationCommandRepository NotificationCommands { get; }
    public INotificationQueryRepository NotificationQueries { get; }
    public IConversationCommandRepository ConversationCommands { get; }
    public IConversationQueryRepository ConversationQueries { get; }
    public IChatMessageCommandRepository ChatMessageCommands { get; }
    public IChatMessageQueryRepository ChatMessageQueries { get; }

    public UnitOfWork(
        ICustomerAddressCommandRepository customerAddressCommands,
        ICustomerAddressQueryRepository customerAddressQueries,
        ICustomerCommandRepository customerCommands,
        ICustomerQueryRepository customerQueries,
        IPropertyInspectionCommandRepository propertyInspectionCommands,
        IPropertyInspectionQueryRepository propertyInspectionQueries,
        IPropertyAddressCommandRepository propertyAddressCommands,
        IPropertyAddressQueryRepository propertyAddressQueries,
        IPropertyFileCommandRepository propertyFileCommands,
        IPropertyFileQueryRepository propertyFileQueries,
        IPropertyCommandRepository propertyCommands,
        IPropertyQueryRepository propertyQueries,
        INotificationCommandRepository notificationCommands,
        INotificationQueryRepository notificationQueries,
        IConversationCommandRepository conversationCommands,
        IConversationQueryRepository conversationQueries,
        IChatMessageCommandRepository chatMessageCommands,
        IChatMessageQueryRepository chatMessageQueries)
    {
        CustomerAddressCommands = customerAddressCommands;
        CustomerAddressQueries = customerAddressQueries;
        CustomerCommands = customerCommands;
        CustomerQueries = customerQueries;
        PropertyInspectionCommands = propertyInspectionCommands;
        PropertyInspectionQueries = propertyInspectionQueries;
        PropertyAddressCommands = propertyAddressCommands;
        PropertyAddressQueries = propertyAddressQueries;
        PropertyFileCommands = propertyFileCommands;
        PropertyFileQueries = propertyFileQueries;
        PropertyCommands = propertyCommands;
        PropertyQueries = propertyQueries;
        NotificationCommands = notificationCommands;
        NotificationQueries = notificationQueries;
        ConversationCommands = conversationCommands;
        ConversationQueries = conversationQueries;
        ChatMessageCommands = chatMessageCommands;
        ChatMessageQueries = chatMessageQueries;
    }

    public Task SaveAsync()
    {
        // DynamoDB operations are persisted immediately in each repository method.
        // Timestamps are set in GenericCommandRepository. This method is kept for interface compatibility.
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // No unmanaged resources to dispose; IDynamoDBContext is managed by DI container.
    }
}
