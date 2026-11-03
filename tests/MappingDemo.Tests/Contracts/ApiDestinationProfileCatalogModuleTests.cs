using MappingDemo.Shared.Contracts;

namespace MappingDemo.Tests.Contracts;

public sealed class ApiDestinationProfileCatalogModuleTests
{
    [Fact]
    public async Task Pinned_cache_uses_only_exact_unexpired_version()
    {
        var versionOne = Profile("partner-a", 11, "a");
        var source = new SwitchableCatalog([versionOne]);
        var time = new ManualTimeProvider();
        var module = new ApiDestinationProfileCatalogModule(
            source,
            ApiDestinationProfileCachePolicy.Default,
            time);

        var loaded = await module.ResolvePinnedAsync(11);
        source.Available = false;
        var cached = await module.ResolvePinnedAsync(11);
        var missing = await module.ResolvePinnedAsync(12);
        var active = await module.ResolveActiveAsync("partner-a");

        Assert.True(loaded.IsSuccess);
        Assert.Same(loaded.Profile, cached.Profile);
        Assert.Equal(
            "ProfileCatalogUnavailable",
            Assert.Single(missing.Diagnostics).Code);
        Assert.Equal(
            "ProfileCatalogUnavailable",
            Assert.Single(active.Diagnostics).Code);

        time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromTicks(1));
        var expired = await module.ResolvePinnedAsync(11);

        Assert.False(expired.IsSuccess);
        Assert.Equal(
            "ProfileCatalogUnavailable",
            Assert.Single(expired.Diagnostics).Code);
    }

    [Fact]
    public async Task Pinned_resolution_rejects_substituted_version()
    {
        var wrongVersion = Profile("partner-a", 12, "b") with
        {
            DeliveryBaseUrl = "https://wrong-endpoint.example.test"
        };
        var source = new SwitchableCatalog([])
        {
            PinnedOverride = wrongVersion
        };
        var module = new ApiDestinationProfileCatalogModule(
            source,
            ApiDestinationProfileCachePolicy.Default,
            TimeProvider.System);

        var result = await module.ResolvePinnedAsync(11);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "InvalidPinnedProfileVersion",
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task Safe_list_cache_is_bounded_and_locally_invalidated()
    {
        var source = new SwitchableCatalog([Profile("partner-a", 11, "a")]);
        var time = new ManualTimeProvider();
        var module = new ApiDestinationProfileCatalogModule(
            source,
            ApiDestinationProfileCachePolicy.Default,
            time);

        var first = await module.GetSafeProfilesAsync();
        source.ReplaceProfiles([Profile("partner-b", 21, "b")]);
        var cached = await module.GetSafeProfilesAsync();
        module.InvalidateSafeProfiles();
        var invalidated = await module.GetSafeProfilesAsync();

        Assert.Equal("partner-a", Assert.Single(first).Id);
        Assert.Equal("partner-a", Assert.Single(cached).Id);
        Assert.Equal("partner-b", Assert.Single(invalidated).Id);
        Assert.Equal(2, source.ListCalls);

        source.ReplaceProfiles([Profile("partner-c", 31, "c")]);
        time.Advance(TimeSpan.FromSeconds(15) + TimeSpan.FromTicks(1));
        var expired = await module.GetSafeProfilesAsync();

        Assert.Equal("partner-c", Assert.Single(expired).Id);
        Assert.Equal(3, source.ListCalls);
    }

    [Fact]
    public async Task Catalog_records_cache_failure_and_latency_metrics()
    {
        var source = new SwitchableCatalog([Profile("partner-a", 11, "a")]);
        var observability = new ApiDestinationProfileObservability();
        var module = new ApiDestinationProfileCatalogModule(
            source,
            ApiDestinationProfileCachePolicy.Default,
            TimeProvider.System,
            observability);

        _ = await module.ResolvePinnedAsync(11);
        _ = await module.ResolvePinnedAsync(11);
        _ = await module.ResolvePinnedAsync(99);

        var snapshot = observability.Snapshot();

        Assert.Contains(snapshot.Cache, metric =>
            metric.Cache == "pinned" &&
            metric.Result == "hit" &&
            metric.Count == 1);
        Assert.Contains(snapshot.Cache, metric =>
            metric.Cache == "pinned" &&
            metric.Result == "miss" &&
            metric.Count == 2);
        Assert.Contains(snapshot.ResolutionFailures, metric =>
            metric.Failure == "pinned_version_missing" &&
            metric.Count == 1);
        Assert.Equal(
            3,
            snapshot.ResolveLatency.Single(metric =>
                metric.Operation == "resolvepinned").Count);
    }

    [Theory]
    [InlineData(null, ApiDestinationProfileCatalogSourceMode.Postgres)]
    [InlineData("configuration", ApiDestinationProfileCatalogSourceMode.Configuration)]
    [InlineData("compare", ApiDestinationProfileCatalogSourceMode.Compare)]
    [InlineData("POSTGRES", ApiDestinationProfileCatalogSourceMode.Postgres)]
    public void Source_mode_is_explicit_and_case_insensitive(
        string? value,
        ApiDestinationProfileCatalogSourceMode expected)
    {
        Assert.Equal(
            expected,
            ApiDestinationProfileCatalogSourceModeParser.Parse(value));
    }

    [Fact]
    public void Unknown_source_mode_fails_startup()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ApiDestinationProfileCatalogSourceModeParser.Parse("Fallback"));
        Assert.Throws<InvalidOperationException>(() =>
            ApiDestinationProfileCatalogSourceModeParser.Parse(" "));
    }

    [Fact]
    public async Task Compare_mode_accepts_canonical_parity_and_uses_declared_authority()
    {
        var configurationProfile = Profile("partner-a", 0, "a") with
        {
            ContentHash = string.Empty
        };
        var postgresProfile = configurationProfile with
        {
            ProfileVersionId = 11,
            ContentHash = ApiDestinationProfileCanonicalizer
                .ComputeContentHash(configurationProfile)
        };
        var catalog = new ComparingApiDestinationProfileCatalog(
            new ApiDestinationProfileCatalog([configurationProfile]),
            new ApiDestinationProfileCatalog([postgresProfile]),
            ApiDestinationProfileCatalogSourceMode.Postgres);
        var rollbackCatalog = new ComparingApiDestinationProfileCatalog(
            new ApiDestinationProfileCatalog([configurationProfile]),
            new ApiDestinationProfileCatalog([postgresProfile]),
            ApiDestinationProfileCatalogSourceMode.Configuration);

        var safe = await catalog.GetSafeProfilesAsync();
        var active = await catalog.ResolveActiveAsync("partner-a");
        var pinned = await catalog.ResolvePinnedAsync(11);
        var rollbackPinned = await rollbackCatalog.ResolvePinnedAsync(11);

        Assert.Equal(11, Assert.Single(safe).ProfileVersionId);
        Assert.Equal(11, active.Profile?.ProfileVersionId);
        Assert.Equal(11, pinned.Profile?.ProfileVersionId);
        Assert.Equal(0, rollbackPinned.Profile?.ProfileVersionId);
        Assert.Equal(
            configurationProfile.DeliveryBaseUrl,
            rollbackPinned.Profile?.DeliveryBaseUrl);
    }

    [Fact]
    public async Task Compare_mode_blocks_canonical_mismatch()
    {
        var configurationProfile = Profile("partner-a", 0, "a") with
        {
            ContentHash = string.Empty
        };
        var postgresProfile = configurationProfile with
        {
            ProfileVersionId = 11,
            DeliveryPathTemplate = "/different/{contractVersion}"
        };
        postgresProfile = postgresProfile with
        {
            ContentHash = ApiDestinationProfileCanonicalizer
                .ComputeContentHash(postgresProfile)
        };
        var catalog = new ComparingApiDestinationProfileCatalog(
            new ApiDestinationProfileCatalog([configurationProfile]),
            new ApiDestinationProfileCatalog([postgresProfile]),
            ApiDestinationProfileCatalogSourceMode.Configuration);

        var active = await catalog.ResolveActiveAsync("partner-a");

        Assert.False(active.IsSuccess);
        Assert.Equal(
            "ProfileCatalogParityMismatch",
            Assert.Single(active.Diagnostics).Code);
        await Assert.ThrowsAsync<ApiDestinationProfileCatalogParityException>(
            async () => await catalog.GetSafeProfilesAsync());
    }

    private static ApiDestinationProfile Profile(
        string id,
        long versionId,
        string hashCharacter) => new(
        id,
        id,
        true,
        "https://contracts.example.test",
        "/schemas/{contractReference}",
        "https://delivery.example.test",
        "/deliveries/{contractVersion}",
        "POST",
        ["Idempotency-Key", "X-Correlation-Id"],
        string.Empty,
        10,
        [new SupportedContract("proposal", "1", "proposal/1")],
        [new PayloadBinding("meta.fileJobId", RuntimeValueSource.FileJobId)],
        ProfileVersionId: versionId,
        ContentHash: $"sha256:{new string(hashCharacter[0], 64)}");

    private sealed class SwitchableCatalog : IApiDestinationProfileCatalog
    {
        private IReadOnlyList<ApiDestinationProfile> _profiles;

        public SwitchableCatalog(IReadOnlyList<ApiDestinationProfile> profiles)
        {
            _profiles = profiles;
        }

        public bool Available { get; set; } = true;

        public ApiDestinationProfile? PinnedOverride { get; init; }

        public int ListCalls { get; private set; }

        public void ReplaceProfiles(IReadOnlyList<ApiDestinationProfile> profiles) =>
            _profiles = profiles;

        public ValueTask<IReadOnlyList<ApiDestinationProfileMetadata>>
            GetSafeProfilesAsync(CancellationToken cancellationToken = default)
        {
            EnsureAvailable();
            ListCalls++;
            IReadOnlyList<ApiDestinationProfileMetadata> result = _profiles
                .Where(profile => profile.Lifecycle == "Active")
                .Select(profile => new ApiDestinationProfileMetadata(
                    profile.Id,
                    profile.DisplayName,
                    profile.Enabled,
                    profile.Method,
                    profile.DeliveryPathTemplate,
                    profile.Environment,
                    profile.AllowedHeaderNames,
                    profile.SupportedContracts,
                    profile.PayloadBindings,
                    profile.ProfileVersionId,
                    profile.VersionNo,
                    profile.Lifecycle,
                    profile.ContentHash))
                .ToArray();
            return ValueTask.FromResult(result);
        }

        public ValueTask<ProfileResolutionResult> ResolveActiveAsync(
            string profileId,
            CancellationToken cancellationToken = default)
        {
            EnsureAvailable();
            var profile = _profiles.SingleOrDefault(value =>
                value.Id == profileId && value.Lifecycle == "Active");
            return ValueTask.FromResult(profile is null
                ? ProfileResolutionResult.Failure(
                    "UnknownProfile",
                    "Profile is unavailable.")
                : ProfileResolutionResult.Success(profile));
        }

        public ValueTask<ProfileResolutionResult> ResolvePinnedAsync(
            long profileVersionId,
            CancellationToken cancellationToken = default)
        {
            EnsureAvailable();
            var profile = PinnedOverride ?? _profiles.SingleOrDefault(value =>
                value.ProfileVersionId == profileVersionId);
            return ValueTask.FromResult(profile is null
                ? ProfileResolutionResult.Failure(
                    "UnknownPinnedProfileVersion",
                    "Profile Version is unavailable.")
                : ProfileResolutionResult.Success(profile));
        }

        private void EnsureAvailable()
        {
            if (!Available)
            {
                throw new InvalidOperationException("Catalog unavailable.");
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
