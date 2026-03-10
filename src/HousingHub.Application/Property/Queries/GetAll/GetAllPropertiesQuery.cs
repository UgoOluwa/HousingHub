using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetAll;

public record GetAllPropertiesQuery(GetAllPropertiesFilterDto Filter) : IRequest<BaseResponsePagination<PaginatedResult<PropertyDto>>>;
