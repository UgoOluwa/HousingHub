using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public interface IMapper
{
    TDestination Map<TDestination>(object source);
    TDestination Map<TSource, TDestination>(TSource source);
}

public class ObjectMapper : IMapper
{
    private readonly TypeAdapterConfig _config;

    public ObjectMapper(TypeAdapterConfig config)
    {
        _config = config;
    }

    public TDestination Map<TDestination>(object source) => source.Adapt<TDestination>(_config);
    public TDestination Map<TSource, TDestination>(TSource source) => source.Adapt<TDestination>(_config);
}
