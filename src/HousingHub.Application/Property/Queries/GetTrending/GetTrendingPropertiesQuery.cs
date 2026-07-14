using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetTrending;

public record GetTrendingPropertiesQuery(int Count = 10, int Skip = 0) : IRequest<BaseResponse<List<PropertyDto>>>;
