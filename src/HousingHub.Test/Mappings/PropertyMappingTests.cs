using System.Reflection;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.Property;
using Mapster;

namespace HousingHub.Test.Mappings;

/// <summary>
/// Guards the mapping that publishing a property depends on. A failure here surfaces
/// as "The type initializer for 'Mapster.TypeAdapter`2' threw an exception" at runtime,
/// which is a 500 with no useful detail for the caller.
/// </summary>
public class PropertyMappingTests
{
    private static TypeAdapterConfig BuildConfig()
    {
        // Same registration the API performs at startup.
        var config = new TypeAdapterConfig();
        config.Scan(Assembly.GetAssembly(typeof(PropertyMapper))!);
        return config;
    }

    private static Property CreateProperty()
    {
        var property = new Property("Test", "Desc", PropertyType.House, 1_000_000m,
            PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            OwnerId = Guid.NewGuid(),
            AddressId = Guid.NewGuid()
        };

        // The navigation properties populated during CreateProperty.
        property.Files.Add(new Model.Entities.PropertyFile("https://example.com/a.jpg", PropertyFileType.Image, 1024)
        {
            PropertyId = property.Id
        });
        property.Address = new Model.Entities.PropertyAddress("Place", "City", "State", "Country", "100001");

        return property;
    }

    [Fact]
    public void PropertyToPropertyDto_Maps()
    {
        var mapper = new ObjectMapper(BuildConfig());

        var dto = mapper.Map<PropertyDto>(CreateProperty());

        Assert.NotNull(dto);
        Assert.Equal("Test", dto.Title);
        Assert.NotNull(dto.Files);
        Assert.Single(dto.Files!);
    }

    [Fact]
    public void PropertyToPropertyDto_MapsWithExplicitSourceType()
    {
        var mapper = new ObjectMapper(BuildConfig());

        var dto = mapper.Map<Property, PropertyDto>(CreateProperty());

        Assert.NotNull(dto);
        Assert.Equal("Test", dto.Title);
    }

    [Fact]
    public void MappingConfiguration_Compiles()
    {
        // Compiles every registered mapping; fails loudly here instead of at runtime.
        var config = BuildConfig();
        config.Compile();
    }

    [Fact]
    public void EveryRegisteredMapper_CompilesInIsolation()
    {
        // Names the offending registration instead of failing on the whole config.
        var registers = Assembly.GetAssembly(typeof(PropertyMapper))!
            .GetTypes()
            .Where(t => typeof(IRegister).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        var failures = new List<string>();

        foreach (var type in registers)
        {
            try
            {
                var config = new TypeAdapterConfig();
                ((IRegister)Activator.CreateInstance(type)!).Register(config);
                config.Compile();
            }
            catch (Exception ex)
            {
                failures.Add($"{type.Name}: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, "Mappers failed to compile:\n" + string.Join("\n", failures));
    }
}
