using HousingHub.Data.Contexts;
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
    public IPropertyInterestCommandRepository PropertyInterestCommands { get; }
    public IPropertyInterestQueryRepository PropertyInterestQueries { get; }
    public IPropertyAddressCommandRepository PropertyAddressCommands { get; }
    public IPropertyAddressQueryRepository PropertyAddressQueries { get; }
    public IPropertyFileCommandRepository PropertyFileCommands { get; }
    public IPropertyFileQueryRepository PropertyFileQueries { get; }
    public IPropertyCommandRepository PropertyCommands { get; }
    public IPropertyQueryRepository PropertyQueries { get; }
    private readonly AppDbContext _applicationContext;


    public UnitOfWork(AppDbContext applicationContext, ICustomerAddressCommandRepository customerAddressCommands, ICustomerAddressQueryRepository customerAddressQueries, ICustomerCommandRepository customerCommands, ICustomerQueryRepository customerQueries, IPropertyInterestCommandRepository propertyInterestCommands, IPropertyInterestQueryRepository propertyInterestQueries, IPropertyAddressCommandRepository propertyAddressCommands, IPropertyAddressQueryRepository propertyAddressQueries, IPropertyFileCommandRepository propertyFileCommands, IPropertyFileQueryRepository propertyFileQueries, IPropertyCommandRepository propertyCommands, IPropertyQueryRepository propertyQueries)
    {
        _applicationContext = applicationContext;
        CustomerAddressCommands = customerAddressCommands;
        CustomerAddressQueries = customerAddressQueries;
        CustomerCommands = customerCommands;
        CustomerQueries = customerQueries;
        PropertyInterestCommands = propertyInterestCommands;
        PropertyInterestQueries = propertyInterestQueries;
        PropertyAddressCommands = propertyAddressCommands;
        PropertyAddressQueries = propertyAddressQueries;
        PropertyFileCommands = propertyFileCommands;
        PropertyFileQueries = propertyFileQueries;
        PropertyCommands = propertyCommands;
        PropertyQueries = propertyQueries;
    }

    public async Task SaveAsync()
    {
        await _applicationContext.SaveChangesAsync();
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _applicationContext.Dispose();
        }
    }

}
