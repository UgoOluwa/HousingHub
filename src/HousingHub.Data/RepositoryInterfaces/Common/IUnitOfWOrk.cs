using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Data.RepositoryInterfaces.Queries;

namespace HousingHub.Data.RepositoryInterfaces.Common;

public interface IUnitOfWOrk : IDisposable
{
    ICustomerAddressCommandRepository CustomerAddressCommands { get; }
    ICustomerAddressQueryRepository CustomerAddressQueries { get; }
    ICustomerCommandRepository CustomerCommands { get; }
    ICustomerQueryRepository CustomerQueries { get; }
    IPropertyInterestCommandRepository PropertyInterestCommands { get; }
    IPropertyInterestQueryRepository PropertyInterestQueries { get; }
    IPropertyAddressCommandRepository PropertyAddressCommands { get; }
    IPropertyAddressQueryRepository PropertyAddressQueries { get; }
    IPropertyFileCommandRepository PropertyFileCommands { get; }
    IPropertyFileQueryRepository PropertyFileQueries { get; }
    IPropertyCommandRepository PropertyCommands { get; }
    IPropertyQueryRepository PropertyQueries { get; }

    Task SaveAsync();
}
