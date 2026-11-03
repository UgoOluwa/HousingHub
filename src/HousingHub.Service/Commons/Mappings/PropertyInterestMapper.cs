using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.Dtos.Notification;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class InspectionMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Every derived member is mapped to nothing on purpose, and the query
        // service fills in the ones it is entitled to afterwards.
        //
        // Left to itself, Mapster flattens an unmatched destination member through
        // the source's navigation properties — PropertyOwnerId becomes
        // Property.OwnerId — and Property and Customer are [DynamoDBIgnore] stubs
        // that are null on every row read from Dynamo. Which members flatten depends
        // on what else is on the two types, so adding a field at either end silently
        // changes whether this throws. It did: adding the completion fields turned
        // every inspection read into a NullReferenceException swallowed by the
        // service's catch, which surfaced as "inspection not found".
        //
        // Mapped to null rather than Ignore()d: InspectionDto is a positional
        // record, so Mapster maps it through its constructor, and ignoring a member
        // leaves the wrong number of arguments.
        config.NewConfig<PropertyInspection, InspectionDto>()
            .Map(dest => dest.PropertyImageUrl, src => (string?)null)
            .Map(dest => dest.PropertyOwnerId, src => (Guid?)null)
            .Map(dest => dest.CustomerName, src => (string?)null)
            .Map(dest => dest.CustomerLocation, src => (string?)null)
            .Map(dest => dest.PropertyOwnerName, src => (string?)null)
            .Map(dest => dest.AssignedStaffName, src => (string?)null);
        config.NewConfig<Notification, NotificationDto>();
    }
}
