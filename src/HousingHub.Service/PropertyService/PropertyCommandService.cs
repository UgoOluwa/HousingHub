using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyService;

public class PropertyCommandService : IPropertyCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyCommandService> _logger;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _fileStorageService;
    private const string ClassName = "property";
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".webm"
    };

    public PropertyCommandService(
        ILogger<PropertyCommandService> logger,
        IUnitOfWOrk unitOfWOrk,
        IMapper mapper,
        IFileStorageService fileStorageService)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _fileStorageService = fileStorageService;
    }

    public async Task<BaseResponse<PropertyDto>> CreateProperty(CreatePropertyDto request, Guid authenticatedUserId)
    {
        try
        {
            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (owner == null)
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (!owner.CustomerType.HasFlag(CustomerType.HouseOwner) && !owner.CustomerType.HasFlag(CustomerType.Agent))
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

            var property = new Property(
                request.Title,
                request.Description,
                request.PropertyType,
                request.Price,
                request.Availability,
                request.PropertyLeaseType)
            {
                OwnerId = authenticatedUserId,
                Features = request.Features,
                ContactPersonName = request.ContactPersonName,
                ContactPersonEmail = request.ContactPersonEmail,
                ContactPersonPhoneNumber = request.ContactPersonPhoneNumber
            };

            if (request.PropertyAddress != null)
            {
                var address = new PropertyAddress(
                    request.PropertyAddress.Place,
                    request.PropertyAddress.City,
                    request.PropertyAddress.State,
                    request.PropertyAddress.Country,
                    request.PropertyAddress.PostalCode);
                property.Address = address;
                property.AddressId = address.Id;
            }

            if (request.Files is { Count: > 0 })
            {
                foreach (var file in request.Files)
                {
                    var validation = ValidateFile(file);
                    if (validation != null)
                        return new BaseResponse<PropertyDto>(null, false, string.Empty, $"{file.FileName}: {validation}");

                    var fileType = ResolveFileType(file);
                    var fileUrl = await _fileStorageService.UploadFileAsync(file, $"properties/{property.Id}");

                    var propertyFile = new PropertyFile(fileUrl, fileType, file.Length)
                    {
                        PropertyId = property.Id
                    };
                    property.Files.Add(propertyFile);
                }
            }

            bool isSuccessful = await _unitOfWOrk.PropertyCommands.InsertAsync(property);
            if (!isSuccessful)
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));

            await _unitOfWOrk.SaveAsync();

            PropertyDto response = _mapper.Map<PropertyDto>(property);
            return new BaseResponse<PropertyDto>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreateProperty: {Message}", ex.Message);
            return new BaseResponse<PropertyDto>(null, false, string.Empty, ex.Message);
        }
    }

    private static string? ValidateFile(IFormFile file)
    {
        if (file.Length > MaxFileSizeBytes)
            return ResponseMessages.FileTooLarge;

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedImageExtensions.Contains(ext) && !AllowedVideoExtensions.Contains(ext))
            return ResponseMessages.InvalidFileType;

        return null;
    }

    private static PropertyFileType ResolveFileType(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        return AllowedVideoExtensions.Contains(ext) ? PropertyFileType.Video : PropertyFileType.Image;
    }

    public async Task<BaseResponse<PropertyDto>> UpdateProperty(UpdatePropertyDto request, Guid authenticatedUserId)
    {
        try
        {
            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (owner == null)
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (!owner.CustomerType.HasFlag(CustomerType.HouseOwner) && !owner.CustomerType.HasFlag(CustomerType.Agent))
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == request.Id,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (property == null)
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (property.OwnerId != authenticatedUserId)
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.PropertyNotOwnedByUser);

            if (request.Title != null) property.Title = request.Title;
            if (request.Description != null) property.Description = request.Description;
            if (request.PropertyType.HasValue) property.PropertyType = request.PropertyType.Value;
            if (request.Price.HasValue) property.Price = request.Price.Value;
            if (request.Availability.HasValue) property.Availability = request.Availability.Value;
            if (request.PropertyLeaseType.HasValue) property.PropertyLeaseType = request.PropertyLeaseType.Value;
            if (request.Features.HasValue) property.Features = request.Features.Value;
            if (request.ContactPersonName != null) property.ContactPersonName = request.ContactPersonName;
            if (request.ContactPersonEmail != null) property.ContactPersonEmail = request.ContactPersonEmail;
            if (request.ContactPersonPhoneNumber != null) property.ContactPersonPhoneNumber = request.ContactPersonPhoneNumber;

            _unitOfWOrk.PropertyCommands.Update(property);
            await _unitOfWOrk.SaveAsync();

            PropertyDto response = _mapper.Map<PropertyDto>(property);
            return new BaseResponse<PropertyDto>(response, true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in UpdateProperty: {Message}", ex.Message);
            return new BaseResponse<PropertyDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> DeleteProperty(Guid propertyId, Guid authenticatedUserId)
    {
        try
        {
            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (owner == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (!owner.CustomerType.HasFlag(CustomerType.HouseOwner) && !owner.CustomerType.HasFlag(CustomerType.Agent))
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == propertyId,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (property == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (property.OwnerId != authenticatedUserId)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PropertyNotOwnedByUser);

            _unitOfWOrk.PropertyCommands.Delete(property);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in DeleteProperty: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }
}
