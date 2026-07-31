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
        // Mirror production exactly: BOTH the Service mappers (ObjectMapper) and the
        // Application mappers (MappingProfile) are scanned into GlobalSettings at
        // startup. Scanning only one assembly hides breakage in the other — which is
        // how the CreatePropertyCommand -> CreatePropertyDto failure slipped through.
        var config = new TypeAdapterConfig();
        config.Scan(
            Assembly.GetAssembly(typeof(PropertyMapper))!,                                  // HousingHub.Service
            Assembly.GetAssembly(typeof(HousingHub.Application.Commons.Mappings.MappingProfile))!); // HousingHub.Application
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
    public void CreatePropertyCommand_MapsToDto()
    {
        // The exact adapt CreatePropertyCommandHandler runs on publish.
        var mapper = new ObjectMapper(BuildConfig());

        // Real uploads, not null: mapping IFormFile -> IFormFile (an interface with no
        // constructor) is what threw "The type initializer for 'Mapster.TypeAdapter`2'"
        // on publish. A null Files list hides the bug.
        var files = new List<Microsoft.AspNetCore.Http.IFormFile> { new FakeFormFile() };

        var command = new HousingHub.Application.Property.Commands.Create.CreatePropertyCommand(
            "Test", "Desc", PropertyType.House, 1_000_000m, PropertyAvailability.Available,
            PropertyLeaseType.Rent, PropertyFeature.None, "Jane", "jane@test.com", "080",
            Guid.NewGuid(),
            new Service.Dtos.PropertyAddress.UpdatePropertyAddressDto("Place", "City", "State", "Country", "100001"),
            files);

        var dto = mapper.Map<Service.Dtos.Property.CreatePropertyDto>(command);

        Assert.NotNull(dto);
        Assert.Equal("Test", dto.Title);
        Assert.Null(dto.Latitude);
        Assert.Null(dto.Longitude);
        // The file instance must pass through untouched, not be reconstructed.
        Assert.NotNull(dto.Files);
        Assert.Single(dto.Files!);
        Assert.Same(files[0], dto.Files![0]);
    }

    private sealed class FakeFormFile : Microsoft.AspNetCore.Http.IFormFile
    {
        public string ContentType => "image/png";
        public string ContentDisposition => "form-data; name=\"Files\"; filename=\"a.png\"";
        public Microsoft.AspNetCore.Http.IHeaderDictionary Headers => new Microsoft.AspNetCore.Http.HeaderDictionary();
        public long Length => 3;
        public string Name => "Files";
        public string FileName => "a.png";
        public void CopyTo(Stream target) { }
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Stream OpenReadStream() => new MemoryStream(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void EveryRegisteredMapper_CompilesInIsolation()
    {
        // Names the offending registration instead of failing on the whole config.
        // Covers both mapper assemblies, since either can poison the shared config.
        var registers = new[]
            {
                Assembly.GetAssembly(typeof(PropertyMapper))!,
                Assembly.GetAssembly(typeof(HousingHub.Application.Commons.Mappings.MappingProfile))!
            }
            .SelectMany(a => a.GetTypes())
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
