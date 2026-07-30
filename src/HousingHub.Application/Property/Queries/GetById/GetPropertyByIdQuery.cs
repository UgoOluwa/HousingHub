using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetById;

public record GetPropertyByIdQuery(Guid Id, Guid? RequesterId = null) : IRequest<BaseResponse<PropertyDto?>>;
