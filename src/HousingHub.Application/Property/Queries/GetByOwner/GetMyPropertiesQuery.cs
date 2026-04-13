using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetByOwner;

public record GetMyPropertiesQuery(Guid OwnerId) : IRequest<BaseResponse<List<PropertyDto>>>;
