using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Commons.Geocoding;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.PropertyAlertService.Interfaces;
using HousingHub.Service.PropertyService.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyService;

public class PropertyCommandService : IPropertyCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyCommandService> _logger;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _fileStorageService;
    private readonly IGeocodingService _geocodingService;
    private readonly IEmailService _emailService;
    private readonly IPropertyAlertPreferenceQueryService _propertyAlertPreferenceQueryService;
    private readonly IRealtimeNotifier _realtimeNotifier;
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
        IFileStorageService fileStorageService,
        IGeocodingService geocodingService,
        IEmailService emailService,
        IPropertyAlertPreferenceQueryService propertyAlertPreferenceQueryService,
        IRealtimeNotifier realtimeNotifier)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _fileStorageService = fileStorageService;
        _geocodingService = geocodingService;
        _emailService = emailService;
        _propertyAlertPreferenceQueryService = propertyAlertPreferenceQueryService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<BaseResponse<CreatePropertyResultDto>> CreateProperty(CreatePropertyDto request, Guid authenticatedUserId, Guid? onBehalfOfOwnerId = null)
    {
        try
        {
            Guid ownerId;

            if (onBehalfOfOwnerId.HasValue)
            {
                // Admin posting on behalf of a managed owner — the acting admin isn't
                // the owner, so the usual "does the caller manage properties" check
                // doesn't apply to authenticatedUserId here; it applies to the target owner.
                var managedOwner = await _unitOfWOrk.CustomerQueries.GetByIdAsync(onBehalfOfOwnerId.Value);
                if (managedOwner == null)
                    return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("owner"));

                if (!managedOwner.CustomerType.CanManageProperties())
                    return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

                if (!managedOwner.IsManagedByHousingHub)
                    return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, ResponseMessages.OwnerNotManagedByHousingHub);

                ownerId = onBehalfOfOwnerId.Value;
            }
            else
            {
                var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                    x => x.Id == authenticatedUserId);

                if (owner == null)
                    return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

                if (!owner.CustomerType.CanManageProperties())
                    return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

                ownerId = authenticatedUserId;
            }

            var property = new Property(
                request.Title,
                request.Description,
                request.PropertyType,
                request.Price,
                request.Availability,
                request.PropertyLeaseType)
            {
                OwnerId = ownerId,
                Features = request.Features,
                ContactPersonName = request.ContactPersonName,
                ContactPersonEmail = request.ContactPersonEmail,
                ContactPersonPhoneNumber = request.ContactPersonPhoneNumber,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            PropertyAddress? address = null;
            if (request.PropertyAddress != null)
            {
                address = new PropertyAddress(
                    request.PropertyAddress.Place,
                    request.PropertyAddress.City,
                    request.PropertyAddress.State,
                    request.PropertyAddress.Country,
                    request.PropertyAddress.PostalCode)
                {
                    PropertyId = property.Id
                };

                // The client never supplies coordinates directly — geocode the address so
                // the property can actually be found via "properties near me", and so the
                // duplicate check below has coordinates to compare against. Best-effort:
                // a geocoding failure shouldn't block property creation.
                if (!property.Latitude.HasValue || !property.Longitude.HasValue)
                {
                    var coordinates = await _geocodingService.GeocodeAsync(address.Place, address.City, address.State, address.Country);
                    if (coordinates.HasValue)
                    {
                        property.Latitude = coordinates.Value.Latitude;
                        property.Longitude = coordinates.Value.Longitude;
                    }
                }

                var possibleDuplicate = await FindPossibleDuplicateAsync(
                    property.Latitude, property.Longitude, address.Place, address.City, address.State);

                if (possibleDuplicate != null)
                {
                    if (!request.ConfirmDuplicate)
                    {
                        var duplicateAddress = await _unitOfWOrk.PropertyAddressQueries.GetByIdAsync(possibleDuplicate.AddressId);
                        var addressText = duplicateAddress != null
                            ? $"{duplicateAddress.Place}, {duplicateAddress.City}, {duplicateAddress.State}"
                            : "an existing listing";
                        var warning = new PossibleDuplicateDto(possibleDuplicate.Id, possibleDuplicate.Title, addressText);
                        return new BaseResponse<CreatePropertyResultDto>(
                            new CreatePropertyResultDto(null, warning), true, string.Empty,
                            "A similar listing already exists at this address.");
                    }

                    property.IsFlaggedDuplicate = true;
                    property.PossibleDuplicateOfPropertyId = possibleDuplicate.Id;
                }

                bool addressSaved = await _unitOfWOrk.PropertyAddressCommands.InsertAsync(address);
                if (!addressSaved)
                    return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage("property address"));

                property.Address = address;
                property.AddressId = address.Id;
            }

            if (request.Files is { Count: > 0 })
            {
                foreach (var file in request.Files)
                {
                    var validation = ValidateFile(file);
                    if (!validation.IsValid)
                        return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, $"{file.FileName}: {validation.Error}");

                    var fileType = ResolveFileType(file);
                    var fileUrl = await _fileStorageService.UploadFileAsync(
                        file, $"properties/{property.Id}", validation.ContentType);

                    var propertyFile = new PropertyFile(fileUrl, fileType, file.Length)
                    {
                        PropertyId = property.Id
                    };
                    property.Files.Add(propertyFile);
                }
            }

            bool isSuccessful = await _unitOfWOrk.PropertyCommands.InsertAsync(property);
            if (!isSuccessful)
                return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));

            if (property.Files.Count > 0)
                await _unitOfWOrk.PropertyFileCommands.InsertRangeAsync(property.Files);

            await _unitOfWOrk.SaveAsync();

            PropertyDto propertyDto = _mapper.Map<PropertyDto>(property);
            return new BaseResponse<CreatePropertyResultDto>(
                new CreatePropertyResultDto(propertyDto, null), true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreateProperty: {Message}", ex.Message);
            return new BaseResponse<CreatePropertyResultDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// Scans existing properties for one at (about) the same address as the given
    /// candidate — either within ~75m by coordinates (roughly a building lot), or,
    /// when coordinates aren't available (geocoding failed), an exact match on the
    /// normalized street/city/state. Returns the matched property, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the most expensive single call in the product and it runs on every
    /// property creation. "Is there anything within 75 metres of this point" cannot be
    /// answered by a key-value store without a spatial key, so the scan of Properties
    /// stands until coordinates are geohashed onto the entity and indexed. That is a
    /// schema change plus a backfill; see the sweep document.
    /// </para>
    /// <para>
    /// What has been fixed is the part that was gratuitous: the fallback branch used to
    /// issue one address read per coordinate-less candidate, sequentially, inside the
    /// request. At a thousand properties with a tenth missing coordinates that was a
    /// hundred serialised round trips on top of the scan. The reads are now batched and
    /// issued together, and only when the fallback is actually reached.
    /// </para>
    /// </remarks>
    private async Task<Property?> FindPossibleDuplicateAsync(double? lat, double? lng, string place, string city, string state)
    {
        const double DuplicateRadiusMeters = 75;
        var candidates = (await _unitOfWOrk.PropertyQueries.GetAllAsync()).ToList();

        if (lat.HasValue && lng.HasValue)
        {
            var byDistance = candidates.FirstOrDefault(p =>
                p.Latitude.HasValue && p.Longitude.HasValue &&
                HaversineDistanceMeters(lat.Value, lng.Value, p.Latitude.Value, p.Longitude.Value) <= DuplicateRadiusMeters);
            if (byDistance != null) return byDistance;
        }

        // AddressId is non-nullable but unset rows read as Guid.Empty, which is not a
        // key anything can be loaded by.
        var withoutCoordinates = candidates
            .Where(p => (!p.Latitude.HasValue || !p.Longitude.HasValue) && p.AddressId != Guid.Empty)
            .ToList();

        if (withoutCoordinates.Count == 0) return null;

        static string NormalizeForComparison(string s) => s.Trim().ToLowerInvariant();
        string normalizedPlace = NormalizeForComparison(place);
        string normalizedCity = NormalizeForComparison(city);
        string normalizedState = NormalizeForComparison(state);

        var addresses = await _unitOfWOrk.PropertyAddressQueries.GetManyByAsync(
            a => a.Id, withoutCoordinates.Select(p => p.AddressId));

        var addressById = addresses.ToDictionary(a => a.Id);

        foreach (var candidate in withoutCoordinates)
        {
            if (!addressById.TryGetValue(candidate.AddressId, out var candidateAddress))
                continue;

            if (NormalizeForComparison(candidateAddress.Place) == normalizedPlace &&
                NormalizeForComparison(candidateAddress.City) == normalizedCity &&
                NormalizeForComparison(candidateAddress.State) == normalizedState)
            {
                return candidate;
            }
        }

        return null;
    }

    private static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusMeters = 6371000;
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    public async Task<BaseResponse<bool>> DismissDuplicateFlagAsync(Guid propertyId)
    {
        try
        {
            var property = await _unitOfWOrk.PropertyQueries.GetByIdAsync(propertyId);
            if (property == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            property.IsFlaggedDuplicate = false;
            property.PossibleDuplicateOfPropertyId = null;
            await _unitOfWOrk.PropertyCommands.UpdateAsync(property);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.DuplicateFlagDismissed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in DismissDuplicateFlagAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>Validates the upload and returns the content type to store it as.</summary>
    private static UploadedFileValidator.Result ValidateFile(IFormFile file)
    {
        var allowed = new HashSet<string>(AllowedImageExtensions, StringComparer.OrdinalIgnoreCase);
        allowed.UnionWith(AllowedVideoExtensions);

        return UploadedFileValidator.Validate(file, allowed, MaxFileSizeBytes);
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
                x => x.Id == authenticatedUserId);

            if (owner == null)
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (!owner.CustomerType.CanManageProperties())
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == request.Id);

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
            if (request.Latitude.HasValue) property.Latitude = request.Latitude.Value;
            if (request.Longitude.HasValue) property.Longitude = request.Longitude.Value;

            await _unitOfWOrk.PropertyCommands.UpdateAsync(property);

            if (request.PropertyAddress != null)
            {
                var existingAddress = await _unitOfWOrk.PropertyAddressQueries.GetByIdAsync(property.AddressId);
                if (existingAddress != null)
                {
                    bool addressChanged =
                        (request.PropertyAddress.Place != null && request.PropertyAddress.Place != existingAddress.Place) ||
                        (request.PropertyAddress.City != null && request.PropertyAddress.City != existingAddress.City) ||
                        (request.PropertyAddress.State != null && request.PropertyAddress.State != existingAddress.State) ||
                        (request.PropertyAddress.Country != null && request.PropertyAddress.Country != existingAddress.Country);

                    if (request.PropertyAddress.Place != null) existingAddress.Place = request.PropertyAddress.Place;
                    if (request.PropertyAddress.City != null) existingAddress.City = request.PropertyAddress.City;
                    if (request.PropertyAddress.State != null) existingAddress.State = request.PropertyAddress.State;
                    if (request.PropertyAddress.Country != null) existingAddress.Country = request.PropertyAddress.Country;
                    if (request.PropertyAddress.PostalCode != null) existingAddress.PostalCode = request.PropertyAddress.PostalCode;

                    await _unitOfWOrk.PropertyAddressCommands.UpdateAsync(existingAddress);

                    // Re-geocode when the address actually changed, or as a one-time backfill
                    // for properties saved before geocoding existed.
                    if (addressChanged || !property.Latitude.HasValue || !property.Longitude.HasValue)
                    {
                        var coordinates = await _geocodingService.GeocodeAsync(
                            existingAddress.Place, existingAddress.City, existingAddress.State, existingAddress.Country);
                        if (coordinates.HasValue)
                        {
                            property.Latitude = coordinates.Value.Latitude;
                            property.Longitude = coordinates.Value.Longitude;
                            await _unitOfWOrk.PropertyCommands.UpdateAsync(property);
                        }
                    }
                }
            }

            await _unitOfWOrk.SaveAsync();

            property.Files = (await _unitOfWOrk.PropertyFileQueries.GetAllAsync(x => x.PropertyId == property.Id)).ToList();

            PropertyDto response = _mapper.Map<PropertyDto>(property);
            return new BaseResponse<PropertyDto>(response, true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in UpdateProperty: {Message}", ex.Message);
            return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> DeleteProperty(Guid propertyId, Guid authenticatedUserId)
    {
        try
        {
            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == authenticatedUserId);
            if (owner == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (!owner.CustomerType.CanManageProperties())
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == propertyId);
            if (property == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (property.OwnerId != authenticatedUserId)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PropertyNotOwnedByUser);

            await _unitOfWOrk.PropertyCommands.DeleteAsync(property);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in DeleteProperty: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public Task<BaseResponse<bool>> SetPropertyPublishedAsync(Guid propertyId, bool isPublished, string? reason = null) =>
        SetPropertyPublishedInternalAsync(propertyId, isPublished, ownerCheck: null, reason);

    public async Task<BaseResponse<bool>> SetPropertyPublishedAsync(Guid propertyId, bool isPublished, Guid authenticatedUserId)
    {
        var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == authenticatedUserId);
        if (owner == null)
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

        if (!owner.CustomerType.CanManageProperties())
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

        // Identity verification gates publishing, not creating.
        //
        // This check previously existed only in the frontend, which redirected an
        // unverified user away from the add-property page. A redirect is a
        // convenience, not a control: POSTing directly to the API — the same request
        // the app itself makes — put a listing live with no identity check at all.
        // Since the entire proposition is that we know who these people are, the
        // control has to live here.
        //
        // Gating publish rather than creation is deliberate. An owner can draft a
        // listing, upload photos and set a price while their documents are in review,
        // and it goes live the moment they are approved. Blocking creation would send
        // a new agent into a wall on day one.
        //
        // Unpublishing is never gated — an owner must always be able to take their own
        // listing down, verified or not.
        if (isPublished && !owner.IsKycVerified)
        {
            return new BaseResponse<bool>(false, false, string.Empty,
                owner.KycSubmittedAt.HasValue
                    ? ResponseMessages.KycRequiredToPublish
                    : ResponseMessages.KycNotSubmitted);
        }

        return await SetPropertyPublishedInternalAsync(propertyId, isPublished, authenticatedUserId, reason: null);
    }

    private async Task<BaseResponse<bool>> SetPropertyPublishedInternalAsync(Guid propertyId, bool isPublished, Guid? ownerCheck, string? reason)
    {
        try
        {
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == propertyId);
            if (property == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (ownerCheck.HasValue && property.OwnerId != ownerCheck.Value)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PropertyNotOwnedByUser);

            bool wasAlreadyPublished = property.IsPublished;
            property.IsPublished = isPublished;
            property.PublishedAt = isPublished ? DateTime.UtcNow : null;
            property.UnpublishReason = isPublished ? null : reason;
            property.DateModified = DateTime.UtcNow;

            await _unitOfWOrk.PropertyCommands.UpdateAsync(property);
            await _unitOfWOrk.SaveAsync();

            // Only the admin-initiated path (ownerCheck is null) unpublishes on someone
            // else's behalf, so only that path needs to notify the owner why.
            if (!isPublished && !ownerCheck.HasValue)
            {
                var owner = await _unitOfWOrk.CustomerQueries.GetByIdAsync(property.OwnerId);
                if (owner != null)
                {
                    await _emailService.SendPropertyUnpublishedAsync(
                        owner.Email, $"{owner.FirstName} {owner.LastName}", property.Title, reason ?? "No reason provided.");
                }
            }

            // A property only becomes discoverable once published (not at raw creation),
            // so this — not CreateProperty — is where saved-search alerts should fire.
            // Guarded to the actual false-to-true transition so re-publishing an
            // already-published property (a no-op toggle) doesn't re-notify everyone.
            if (isPublished && !wasAlreadyPublished)
            {
                await NotifyMatchingAlertPreferencesAsync(property);
            }

            var message = isPublished ? "Property published successfully." : "Property unpublished successfully.";
            return new BaseResponse<bool>(true, true, string.Empty, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in SetPropertyPublishedAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>Notifies every customer whose saved search matches the just-published property — in-app notification plus a best-effort email.</summary>
    private async Task NotifyMatchingAlertPreferencesAsync(Property property)
    {
        try
        {
            var preferences = await _propertyAlertPreferenceQueryService.GetAllActiveAsync();
            if (preferences.Count == 0) return;

            var address = await _unitOfWOrk.PropertyAddressQueries.GetByIdAsync(property.AddressId);
            var matches = preferences.Where(p => p.Matches(property, address?.City, address?.State)).ToList();
            if (matches.Count == 0) return;

            string formattedAddress = address != null ? $"{address.Place}, {address.City}, {address.State}" : "N/A";

            // One batched read instead of one per match. A listing in a popular area can
            // match hundreds of saved searches, and these reads used to happen one after
            // another inside the publish request — the owner sat waiting for every one.
            var customers = await _unitOfWOrk.CustomerQueries.GetManyByAsync(
                c => c.Id, matches.Select(m => m.CustomerId));
            var customerById = customers.ToDictionary(c => c.Id);

            var recipients = new List<Customer>(matches.Count);

            foreach (var preference in matches)
            {
                if (!customerById.TryGetValue(preference.CustomerId, out var customer)) continue;

                var notification = new Notification(
                    customer.Id, NotificationType.PropertyMatch,
                    "New Property Match",
                    $"A new listing matches your saved search: \"{property.Title}\" in {address?.City ?? "N/A"}.",
                    property.Id);

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);

                var notificationDto = new NotificationDto(
                    notification.Id, notification.DateCreated, notification.RecipientId, notification.InspectionId,
                    notification.Type, notification.Title, notification.Message, notification.IsRead, notification.PropertyId);
                await _realtimeNotifier.SendNotificationAsync(customer.Id, notificationDto);

                recipients.Add(customer);
            }

            await _unitOfWOrk.SaveAsync();

            await SendAlertEmailsAsync(recipients, property, formattedAddress);
        }
        catch (Exception ex)
        {
            // Best-effort — a failure here shouldn't undo the publish that already succeeded.
            _logger.LogError(ex, "An error occurred in NotifyMatchingAlertPreferencesAsync: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Maximum alert emails in flight at once.
    /// </summary>
    /// <remarks>
    /// Sequential sends meant the owner's publish request waited on one HTTP round trip
    /// to Resend per matched saved search. Unbounded parallelism would fix the latency
    /// and immediately trip Resend's rate limit instead, failing the whole batch. Eight
    /// is a deliberately unambitious number: it turns a hundred serial calls into
    /// thirteen rounds, which is enough of a win to matter and slow enough not to be the
    /// thing that gets us throttled.
    /// </remarks>
    private const int AlertEmailConcurrency = 8;

    /// <summary>
    /// Sends the match email to each recipient, a few at a time. Individual failures are
    /// logged and skipped — a bounced address must not cost the other recipients theirs.
    /// </summary>
    /// <remarks>
    /// This still runs inside the publish request. The right home for it is a queue, so
    /// that publishing returns as soon as the listing is live and the fan-out happens on
    /// its own time with its own retries. That needs infrastructure this codebase does
    /// not have yet; until then, bounded parallelism keeps the cost sub-linear.
    /// </remarks>
    private async Task SendAlertEmailsAsync(
        IReadOnlyList<Customer> recipients, Property property, string formattedAddress)
    {
        for (int offset = 0; offset < recipients.Count; offset += AlertEmailConcurrency)
        {
            var batch = recipients.Skip(offset).Take(AlertEmailConcurrency);

            await Task.WhenAll(batch.Select(async customer =>
            {
                try
                {
                    await _emailService.SendPropertyAlertMatchAsync(
                        customer.Email, customer.FirstName, property.Title, formattedAddress, property.Price);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send property-alert-match email to {Email}", customer.Email);
                }
            }));
        }
    }

    public async Task<BaseResponse<bool>> AdminDeletePropertyAsync(Guid propertyId, string reason)
    {
        try
        {
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == propertyId);
            if (property == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var owner = await _unitOfWOrk.CustomerQueries.GetByIdAsync(property.OwnerId);

            await _unitOfWOrk.PropertyCommands.DeleteAsync(property);
            await _unitOfWOrk.SaveAsync();

            if (owner != null)
            {
                await _emailService.SendPropertyDeletedAsync(
                    owner.Email, $"{owner.FirstName} {owner.LastName}", property.Title, reason);
            }

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in AdminDeletePropertyAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> SetPropertyVerifiedAsync(Guid propertyId, bool isVerified)
    {
        try
        {
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == propertyId);
            if (property == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            property.IsVerified = isVerified;
            property.VerifiedAt = isVerified ? DateTime.UtcNow : null;
            property.DateModified = DateTime.UtcNow;

            await _unitOfWOrk.PropertyCommands.UpdateAsync(property);
            await _unitOfWOrk.SaveAsync();

            if (isVerified)
            {
                var owner = await _unitOfWOrk.CustomerQueries.GetByIdAsync(property.OwnerId);
                if (owner != null)
                {
                    await _emailService.SendPropertyVerifiedAsync(
                        owner.Email, $"{owner.FirstName} {owner.LastName}", property.Title);
                }
            }

            var message = isVerified ? "Property verified successfully." : "Property unverified successfully.";
            return new BaseResponse<bool>(true, true, string.Empty, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in SetPropertyVerifiedAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }
}
