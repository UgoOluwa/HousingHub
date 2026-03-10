using HousingHub.Model.Entities;

namespace HousingHub.Service.Commons.Authentication;

public interface ITokenProvider
{
    string Create(Customer user);
}
