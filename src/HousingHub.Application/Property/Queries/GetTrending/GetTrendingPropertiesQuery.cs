using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetTrending;

public record GetTrendingPropertiesQuery(int Count = 10) : IRequest<BaseResponse<List<PropertyDto>>>;
