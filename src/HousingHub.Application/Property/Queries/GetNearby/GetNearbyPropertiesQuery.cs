using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetNearby;

public record GetNearbyPropertiesQuery(
    double Latitude,
    double Longitude,
    double RadiusKm = 10,
    int Count = 10) : IRequest<BaseResponse<List<PropertyDto>>>;
