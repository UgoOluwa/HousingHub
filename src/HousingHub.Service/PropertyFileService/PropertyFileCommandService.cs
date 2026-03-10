using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Dtos.PropertyFile;
using HousingHub.Service.PropertyFileService.Interfaces;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyFileService;

public class PropertyFileCommandService : IPropertyFileCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyFileCommandService> _logger;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _fileStorageService;
    private const string ClassName = "property file";
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".webm"
    };

    public PropertyFileCommandService(
        ILogger<PropertyFileCommandService> logger,
        IUnitOfWOrk unitOfWOrk,
        IMapper mapper,
        IFileStorageService fileStorageService)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _fileStorageService = fileStorageService;
    }

    public async Task<BaseResponse<PropertyFileDto>> CreatePropertyFile(CreatePropertyFileDto request)
    {
        try
        {
            var validation = ValidateFile(request.file);
            if (validation != null)
                return new BaseResponse<PropertyFileDto>(null, false, string.Empty, validation);

            var fileType = ResolveFileType(request.file);
            var fileUrl = await _fileStorageService.UploadFileAsync(request.file, $"properties/{request.PropertyId}");

            var entity = new PropertyFile(fileUrl, fileType, request.file.Length)
            {
                PropertyId = request.PropertyId
            };

            bool isSuccessful = await _unitOfWOrk.PropertyFileCommands.InsertAsync(entity);
            if (!isSuccessful)
                return new BaseResponse<PropertyFileDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));

            await _unitOfWOrk.SaveAsync();
            PropertyFileDto response = _mapper.Map<PropertyFileDto>(entity);
            return new BaseResponse<PropertyFileDto>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreatePropertyFile: {Message}", ex.Message);
            return new BaseResponse<PropertyFileDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<PropertyFileDto>>> UploadPropertyFiles(Guid propertyId, Guid authenticatedUserId, IList<IFormFile> files)
    {
        try
        {
            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (owner == null)
                return new BaseResponse<List<PropertyFileDto>>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (!owner.CustomerType.HasFlag(CustomerType.HouseOwner) && !owner.CustomerType.HasFlag(CustomerType.Agent))
                return new BaseResponse<List<PropertyFileDto>>(null, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == propertyId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (property == null)
                return new BaseResponse<List<PropertyFileDto>>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));

            if (property.OwnerId != authenticatedUserId)
                return new BaseResponse<List<PropertyFileDto>>(null, false, string.Empty, ResponseMessages.PropertyNotOwnedByUser);

            var uploadedFiles = new List<PropertyFile>();

            foreach (var file in files)
            {
                var validation = ValidateFile(file);
                if (validation != null)
                    return new BaseResponse<List<PropertyFileDto>>(null, false, string.Empty, $"{file.FileName}: {validation}");

                var fileType = ResolveFileType(file);
                var fileUrl = await _fileStorageService.UploadFileAsync(file, $"properties/{propertyId}");

                var entity = new PropertyFile(fileUrl, fileType, file.Length)
                {
                    PropertyId = propertyId
                };
                uploadedFiles.Add(entity);
            }

            await _unitOfWOrk.PropertyFileCommands.InsertRangeAsync(uploadedFiles);
            await _unitOfWOrk.SaveAsync();

            var response = _mapper.Map<List<PropertyFileDto>>(uploadedFiles);
            return new BaseResponse<List<PropertyFileDto>>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in UploadPropertyFiles: {Message}", ex.Message);
            return new BaseResponse<List<PropertyFileDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> DeletePropertyFile(Guid fileId, Guid authenticatedUserId)
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

            var file = await _unitOfWOrk.PropertyFileQueries.GetByAsync(x => x.Id == fileId);
            if (file == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == file.PropertyId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (property == null || property.OwnerId != authenticatedUserId)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PropertyNotOwnedByUser);

            await _fileStorageService.DeleteFileAsync(file.FileUrl);
            _unitOfWOrk.PropertyFileCommands.Delete(file);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in DeletePropertyFile: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
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
}
