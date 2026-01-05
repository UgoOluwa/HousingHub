namespace HousingHub.Service.Dtos.PropertyInterest;

public record PropertyInterestDto(Guid Id, DateTime DateCreated, DateTime DateModified, Guid CustomerId, Guid PropertyId);
