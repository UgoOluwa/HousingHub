using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetByOwner;

public record GetMyPropertiesQuery(Guid OwnerId, GetMyPropertiesFilterDto Filter) : IRequest<BaseResponse<HousingHub.Core.CustomResponses.PaginatedResult<PropertyDto>>>;

