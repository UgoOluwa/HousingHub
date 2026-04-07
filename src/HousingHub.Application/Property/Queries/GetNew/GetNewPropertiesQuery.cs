using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetNew;

public record GetNewPropertiesQuery(int Count = 10) : IRequest<BaseResponse<List<PropertyDto>>>;
