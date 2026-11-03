using System.Collections.Concurrent;

namespace MappingDemo.Shared.Contracts;

public enum ApiDestinationProfileCatalogSourceMode
{
    Configuration,
    Compare,
    Postgres
}

public static class ApiDestinationProfileCatalogSourceModeParser
{
    public static ApiDestinationProfileCatalogSourceMode Parse(string? value)
    {
        if (value is null)
        {
            return ApiDestinationProfileCatalogSourceMode.Postgres;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "API Destination Profile catalog source mode cannot be empty.");
        }

        return Enum.TryParse<ApiDestinationProfileCatalogSourceMode>(
            value,
            ignoreCase: true,
            out var mode)
            ? mode
            : throw new InvalidOperationException(
                $"API Destination Profile catalog source mode '{value}' is invalid.");
    }
}

public sealed class ComparingApiDestinationProfileCatalog(
    IApiDestinationProfileCatalog configuration,
    IApiDestinationProfileCatalog postgres,
    ApiDestinationProfileCatalogSourceMode authoritativeSource) :
    IApiDestinationProfileCatalog
{
    public async ValueTask<IReadOnlyList<ApiDestinationProfileMetadata>>
        GetSafeProfilesAsync(CancellationToken cancellationToken = default)
    {
        var configurationProfiles = await configuration.GetSafeProfilesAsync(
            cancellationToken);
        var postgresProfiles = await postgres.GetSafeProfilesAsync(
            cancellationToken);
        var configurationIds = configurationProfiles
            .Select(profile => profile.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var postgresIds = postgresProfiles
            .Select(profile => profile.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!configurationIds.SequenceEqual(postgresIds, StringComparer.Ordinal))
        {
            throw new ApiDestinationProfileCatalogParityException();
        }

        foreach (var profileId in configurationIds)
        {
            var result = await ResolveActivePairAsync(
                profileId,
                cancellationToken);
            if (!result.Configuration.IsSuccess)
            {
                throw new ApiDestinationProfileCatalogParityException();
            }
        }

        return authoritativeSource switch
        {
            ApiDestinationProfileCatalogSourceMode.Configuration =>
                configurationProfiles,
            ApiDestinationProfileCatalogSourceMode.Postgres => postgresProfiles,
            _ => throw InvalidAuthority()
        };
    }

    public async ValueTask<ProfileResolutionResult> ResolveActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var pair = await ResolveActivePairAsync(profileId, cancellationToken);
        return Select(pair.Configuration, pair.Postgres);
    }

    public async ValueTask<ProfileResolutionResult> ResolvePinnedAsync(
        long profileVersionId,
        CancellationToken cancellationToken = default)
    {
        var postgresResult = await postgres.ResolvePinnedAsync(
            profileVersionId,
            cancellationToken);
        if (!postgresResult.IsSuccess)
        {
            return ParityMismatch();
        }

        var configurationResult = await configuration.ResolveActiveAsync(
            postgresResult.Profile!.Id,
            cancellationToken);
        if (!AreEquivalent(configurationResult, postgresResult))
        {
            return ParityMismatch();
        }

        return Select(configurationResult, postgresResult);
    }

    private async ValueTask<ProfilePair> ResolveActivePairAsync(
        string profileId,
        CancellationToken cancellationToken)
    {
        var configurationResult = await configuration.ResolveActiveAsync(
            profileId,
            cancellationToken);
        var postgresResult = await postgres.ResolveActiveAsync(
            profileId,
            cancellationToken);
        return AreEquivalent(configurationResult, postgresResult)
            ? new ProfilePair(configurationResult, postgresResult)
            : new ProfilePair(ParityMismatch(), ParityMismatch());
    }

    private ProfileResolutionResult Select(
        ProfileResolutionResult configurationResult,
        ProfileResolutionResult postgresResult) => authoritativeSource switch
        {
            ApiDestinationProfileCatalogSourceMode.Configuration =>
                configurationResult,
            ApiDestinationProfileCatalogSourceMode.Postgres => postgresResult,
            _ => throw InvalidAuthority()
        };

    private static bool AreEquivalent(
        ProfileResolutionResult configurationResult,
        ProfileResolutionResult postgresResult)
    {
        if (configurationResult.IsSuccess != postgresResult.IsSuccess)
        {
            return false;
        }

        if (!configurationResult.IsSuccess)
        {
            return configurationResult.Diagnostics.Select(item => item.Code)
                .SequenceEqual(
                    postgresResult.Diagnostics.Select(item => item.Code),
                    StringComparer.Ordinal);
        }

        var configurationProfile = configurationResult.Profile!;
        var postgresProfile = postgresResult.Profile!;
        var configurationHash =
            ApiDestinationProfileCanonicalizer.ComputeContentHash(
                configurationProfile);
        var postgresHash =
            ApiDestinationProfileCanonicalizer.ComputeContentHash(
                postgresProfile);
        return configurationProfile.Id == postgresProfile.Id &&
            configurationHash == postgresHash &&
            (string.IsNullOrWhiteSpace(postgresProfile.ContentHash) ||
             postgresProfile.ContentHash == postgresHash);
    }

    private static ProfileResolutionResult ParityMismatch() =>
        ProfileResolutionResult.Failure(
            "ProfileCatalogParityMismatch",
            "The configuration and PostgreSQL profile catalogs do not match.");

    private static InvalidOperationException InvalidAuthority() => new(
        "Compare mode authority must be Configuration or Postgres.");

    private sealed record ProfilePair(
        ProfileResolutionResult Configuration,
        ProfileResolutionResult Postgres);
}

public sealed class ApiDestinationProfileCatalogParityException()
    : Exception("The configuration and PostgreSQL profile catalogs do not match.");

public sealed record ApiDestinationProfileCachePolicy
{
    public static ApiDestinationProfileCachePolicy Default { get; } = new(
        TimeSpan.FromMinutes(5),
        TimeSpan.FromSeconds(15));

    public ApiDestinationProfileCachePolicy(
        TimeSpan pinnedTimeToLive,
        TimeSpan safeListTimeToLive)
    {
        if (pinnedTimeToLive <= TimeSpan.Zero ||
            pinnedTimeToLive > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pinnedTimeToLive),
                "Pinned cache TTL must be greater than zero and at most five minutes.");
        }

        if (safeListTimeToLive <= TimeSpan.Zero ||
            safeListTimeToLive > TimeSpan.FromSeconds(15))
        {
            throw new ArgumentOutOfRangeException(
                nameof(safeListTimeToLive),
                "Safe-list cache TTL must be greater than zero and at most 15 seconds.");
        }

        PinnedTimeToLive = pinnedTimeToLive;
        SafeListTimeToLive = safeListTimeToLive;
    }

    public TimeSpan PinnedTimeToLive { get; }

    public TimeSpan SafeListTimeToLive { get; }
}

public interface IApiDestinationProfileCatalogCacheInvalidator
{
    void InvalidateSafeProfiles();
}

public sealed class ApiDestinationProfileCatalogModule(
    IApiDestinationProfileCatalog source,
    ApiDestinationProfileCachePolicy cachePolicy,
    TimeProvider timeProvider,
    ApiDestinationProfileObservability? observability = null) :
    IApiDestinationProfileCatalog,
    IApiDestinationProfileCatalogCacheInvalidator
{
    private readonly ConcurrentDictionary<long, PinnedCacheEntry> _pinned = [];
    private SafeListCacheEntry? _safeList;

    public async ValueTask<IReadOnlyList<ApiDestinationProfileMetadata>>
        GetSafeProfilesAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = timeProvider.GetTimestamp();
        var now = timeProvider.GetUtcNow();
        var cached = Volatile.Read(ref _safeList);
        if (cached is not null && cached.ExpiresAt > now)
        {
            observability?.RecordCacheAccess(
                ApiDestinationProfileCacheKind.SafeList,
                hit: true);
            RecordLatency(ApiDestinationProfileCatalogOperation.SafeList);
            return cached.Profiles;
        }
        observability?.RecordCacheAccess(
            ApiDestinationProfileCacheKind.SafeList,
            hit: false);

        try
        {
            var profiles = (await source.GetSafeProfilesAsync(cancellationToken))
                .ToArray();
            Interlocked.Exchange(
                ref _safeList,
                new SafeListCacheEntry(
                    profiles,
                    now + cachePolicy.SafeListTimeToLive));
            return profiles;
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            observability?.RecordResolutionFailure(
                exception is ApiDestinationProfileCatalogParityException
                    ? "ProfileCatalogParityMismatch"
                    : "ProfileCatalogUnavailable");
            throw new ApiDestinationProfileCatalogUnavailableException(exception);
        }
        finally
        {
            RecordLatency(ApiDestinationProfileCatalogOperation.SafeList);
        }

        void RecordLatency(ApiDestinationProfileCatalogOperation operation) =>
            observability?.RecordResolutionLatency(
                operation,
                timeProvider.GetElapsedTime(startedAt));
    }

    public async ValueTask<ProfileResolutionResult> ResolveActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var startedAt = timeProvider.GetTimestamp();
        ProfileResolutionResult result;
        try
        {
            result = await source.ResolveActiveAsync(
                profileId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            observability?.RecordResolutionFailure(
                "ProfileCatalogUnavailable");
            return Unavailable();
        }
        finally
        {
            observability?.RecordResolutionLatency(
                ApiDestinationProfileCatalogOperation.ResolveActive,
                timeProvider.GetElapsedTime(startedAt));
        }

        if (!result.IsSuccess)
        {
            RecordFailure(result);
            return result;
        }

        var profile = result.Profile!;
        var validated = profile.Id == profileId &&
            profile.Enabled &&
            profile.Lifecycle == "Active"
            ? result
            : ProfileResolutionResult.Failure(
                "InvalidActiveProfileVersion",
                "The active API destination profile state is inconsistent.");
        RecordFailure(validated);
        return validated;
    }

    public async ValueTask<ProfileResolutionResult> ResolvePinnedAsync(
        long profileVersionId,
        CancellationToken cancellationToken = default)
    {
        var startedAt = timeProvider.GetTimestamp();
        var now = timeProvider.GetUtcNow();
        if (_pinned.TryGetValue(profileVersionId, out var cached))
        {
            if (cached.ExpiresAt > now &&
                cached.ContentHash == cached.Profile.ContentHash)
            {
                observability?.RecordCacheAccess(
                    ApiDestinationProfileCacheKind.Pinned,
                    hit: true);
                observability?.RecordResolutionLatency(
                    ApiDestinationProfileCatalogOperation.ResolvePinned,
                    timeProvider.GetElapsedTime(startedAt));
                return ProfileResolutionResult.Success(cached.Profile);
            }

            _pinned.TryRemove(
                new KeyValuePair<long, PinnedCacheEntry>(
                    profileVersionId,
                    cached));
        }
        observability?.RecordCacheAccess(
            ApiDestinationProfileCacheKind.Pinned,
            hit: false);

        ProfileResolutionResult result;
        try
        {
            result = await source.ResolvePinnedAsync(
                profileVersionId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            observability?.RecordResolutionFailure(
                "ProfileCatalogUnavailable");
            return Unavailable();
        }
        finally
        {
            observability?.RecordResolutionLatency(
                ApiDestinationProfileCatalogOperation.ResolvePinned,
                timeProvider.GetElapsedTime(startedAt));
        }

        if (!result.IsSuccess)
        {
            RecordFailure(result);
            return result;
        }

        var profile = result.Profile!;
        if (profile.ProfileVersionId != profileVersionId ||
            profile.Lifecycle is not ("Active" or "Disabled"))
        {
            var invalid = ProfileResolutionResult.Failure(
                "InvalidPinnedProfileVersion",
                "The pinned API destination profile state is inconsistent.");
            RecordFailure(invalid);
            return invalid;
        }

        if (!string.IsNullOrWhiteSpace(profile.ContentHash))
        {
            _pinned[profileVersionId] = new PinnedCacheEntry(
                profile,
                profile.ContentHash,
                now + cachePolicy.PinnedTimeToLive);
        }

        return result;
    }

    public void InvalidateSafeProfiles() =>
        Interlocked.Exchange(ref _safeList, null);

    private static ProfileResolutionResult Unavailable() =>
        ProfileResolutionResult.Failure(
            "ProfileCatalogUnavailable",
            "The API destination profile catalog is unavailable.");

    private void RecordFailure(ProfileResolutionResult result)
    {
        if (!result.IsSuccess && result.Diagnostics.Count > 0)
        {
            observability?.RecordResolutionFailure(
                result.Diagnostics[0].Code);
        }
    }

    private sealed record PinnedCacheEntry(
        ApiDestinationProfile Profile,
        string ContentHash,
        DateTimeOffset ExpiresAt);

    private sealed record SafeListCacheEntry(
        IReadOnlyList<ApiDestinationProfileMetadata> Profiles,
        DateTimeOffset ExpiresAt);
}

public sealed class ApiDestinationProfileCatalogUnavailableException(
    Exception innerException)
    : Exception(
        "The API destination profile catalog is unavailable.",
        innerException);
