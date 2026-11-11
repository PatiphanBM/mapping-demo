using System.Text.Json;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.DatabaseDestinations;

namespace MappingDemo.Shared.MappingConfigs;

public static class DestinationDefinitionValidator
{
    private const int MaximumPathPartLength = 180;

    private static readonly IReadOnlySet<string> AllowedFileNameTokens =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "sourceBaseName",
            "fileJobId",
            "configVersion",
            "configVersionId",
            "artifactRevision"
        };

    private static readonly IReadOnlySet<string> ReservedWindowsNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5",
            "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
            "LPT6", "LPT7", "LPT8", "LPT9"
        };

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ConfigVersionValidationError> Validate(
        DestinationDefinition destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var element = JsonSerializer.SerializeToElement(
            destination,
            JsonOptions);

        return Validate(element);
    }

    public static IReadOnlyList<ConfigVersionValidationError> Validate(
        JsonElement destination)
    {
        if (destination.ValueKind != JsonValueKind.Object)
        {
            return Error(
                "destination",
                "Destination must be a JSON object.");
        }

        if (!TryGetRequiredString(destination, "kind", out var kind))
        {
            return Error(
                "destination.kind",
                "Destination kind is required.");
        }

        return kind switch
        {
            "legacyTable" => ValidateLegacyTable(destination),
            "apiContract" => ValidateApiContract(destination),
            "jsonFile" => ValidateJsonFile(destination),
            "databaseTable" => ValidateDatabaseTable(destination),
            _ => Error(
                "destination.kind",
                "Unknown destination kind.")
        };
    }

    private static IReadOnlyList<ConfigVersionValidationError>
        ValidateDatabaseTable(JsonElement destination)
    {
        var errors = new List<ConfigVersionValidationError>();
        errors.AddRange(ValidateAllowedProperties(
            destination,
            "databaseTable",
            "kind",
            "profileId",
            "schema",
            "table",
            "writeMode",
            "duplicatePolicy",
            "schemaHash",
            "schemaSnapshot"));
        if (errors.Count > 0)
        {
            return errors;
        }

        foreach (var name in new[] { "profileId", "schema", "table", "schemaHash" })
        {
            if (!TryGetRequiredString(destination, name, out _))
            {
                errors.Add(new ConfigVersionValidationError(
                    $"destination.{name}",
                    $"{name} is required."));
            }
        }
        if (!TryGetRequiredString(destination, "writeMode", out var writeMode) ||
            writeMode != "insert")
        {
            errors.Add(new ConfigVersionValidationError(
                "destination.writeMode",
                "Write mode must be insert."));
        }

        DatabaseTableDestination? typed = null;
        try
        {
            typed = JsonSerializer.Deserialize<DestinationDefinition>(
                destination,
                JsonOptions) as DatabaseTableDestination;
        }
        catch (JsonException)
        {
            errors.Add(new ConfigVersionValidationError(
                "destination",
                "Database table destination is invalid."));
        }

        if (typed is null)
        {
            return errors;
        }

        var snapshot = typed.SchemaSnapshot;
        var computed = TargetTableSchema.Create(
            snapshot.ProfileId,
            snapshot.Provider,
            snapshot.Schema,
            snapshot.Table,
            snapshot.Columns,
            snapshot.UniqueConstraints);
        if (!string.Equals(typed.SchemaHash, snapshot.SchemaHash, StringComparison.Ordinal) ||
            !string.Equals(typed.SchemaHash, computed.SchemaHash, StringComparison.Ordinal) ||
            !string.Equals(typed.ProfileId, snapshot.ProfileId, StringComparison.Ordinal) ||
            !string.Equals(typed.Schema, snapshot.Schema, StringComparison.Ordinal) ||
            !string.Equals(typed.Table, snapshot.Table, StringComparison.Ordinal))
        {
            errors.Add(new ConfigVersionValidationError(
                "destination.schemaHash",
                "Schema snapshot identity or hash is invalid."));
        }

        if (typed.DuplicatePolicy is ExistingUniqueKeyDuplicatePolicy key)
        {
            var keyExists = snapshot.UniqueConstraints.Any(constraint =>
                constraint.Columns.SequenceEqual(key.Columns, StringComparer.Ordinal));
            var columns = snapshot.Columns.ToDictionary(
                column => column.Name,
                StringComparer.Ordinal);
            var keyWritable = key.Columns.Count > 0 && key.Columns.All(column =>
                columns.TryGetValue(column, out var definition) &&
                definition.IsWritable &&
                !definition.IsIdentity &&
                !definition.IsGenerated &&
                !definition.IsComputed);
            if (!keyExists || !keyWritable)
            {
                errors.Add(new ConfigVersionValidationError(
                    "destination.duplicatePolicy.columns",
                    "Selected columns must be an existing writable non-generated unique key."));
            }
        }

        if (snapshot.Columns.Any(column =>
                column.IsRequired &&
                column.ScalarType is DatabaseScalarType.Binary or
                    DatabaseScalarType.Unsupported))
        {
            errors.Add(new ConfigVersionValidationError(
                "destination.schemaSnapshot.columns",
                "A required target column has an unsupported scalar type."));
        }

        return errors;
    }

    private static IReadOnlyList<ConfigVersionValidationError>
        ValidateLegacyTable(JsonElement destination)
    {
        var propertyError = ValidateAllowedProperties(
            destination,
            "legacyTable",
            "kind",
            "normalizedTableId");
        if (propertyError.Count > 0)
        {
            return propertyError;
        }

        if (!destination.TryGetProperty(
                "normalizedTableId",
                out var normalizedTableId) ||
            !normalizedTableId.TryGetInt64(out var value) ||
            value <= 0)
        {
            return Error(
                "destination.normalizedTableId",
                "Normalized table ID must be a positive integer.");
        }

        return [];
    }

    private static IReadOnlyList<ConfigVersionValidationError>
        ValidateApiContract(JsonElement destination)
    {
        var propertyError = ValidateAllowedProperties(
            destination,
            "apiContract",
            "kind",
            "apiDestinationProfileId",
            "contract");
        if (propertyError.Count > 0)
        {
            return propertyError;
        }

        if (!TryGetRequiredString(
                destination,
                "apiDestinationProfileId",
                out _))
        {
            return Error(
                "destination.apiDestinationProfileId",
                "API destination profile ID is required.");
        }

        var contractErrors = ValidateContractReference(destination);
        if (contractErrors.Count > 0)
        {
            return contractErrors;
        }

        return [];
    }

    private static IReadOnlyList<ConfigVersionValidationError>
        ValidateJsonFile(JsonElement destination)
    {
        var propertyError = ValidateAllowedProperties(
            destination,
            "jsonFile",
            "kind",
            "fileDestinationProfileId",
            "contract",
            "relativeFolder",
            "fileNameTemplate",
            "formatting");
        if (propertyError.Count > 0)
        {
            return propertyError;
        }

        if (!TryGetRequiredString(
                destination,
                "fileDestinationProfileId",
                out var profileId))
        {
            return Error(
                "destination.fileDestinationProfileId",
                "File destination profile ID is required.");
        }
        if (!IsLogicalIdentifier(profileId))
        {
            return Error(
                "destination.fileDestinationProfileId",
                "File destination profile ID must be a logical identifier.");
        }

        var contractErrors = ValidateContractReference(destination);
        if (contractErrors.Count > 0)
        {
            return contractErrors;
        }

        if (!TryGetRequiredString(
                destination,
                "relativeFolder",
                out var relativeFolder) ||
            !IsSafeRelativeFolder(relativeFolder))
        {
            return Error(
                "destination.relativeFolder",
                "Relative folder must be a safe relative path.");
        }

        if (!TryGetRequiredString(
                destination,
                "fileNameTemplate",
                out var fileNameTemplate))
        {
            return Error(
                "destination.fileNameTemplate",
                "File name template is required.");
        }
        if (fileNameTemplate.Contains('/') ||
            fileNameTemplate.Contains('\\'))
        {
            return Error(
                "destination.fileNameTemplate",
                "File name template must not contain path separators.");
        }
        if (!HasOnlyAllowedFileNameTokens(fileNameTemplate))
        {
            return Error(
                "destination.fileNameTemplate",
                "File name template contains an unknown token.");
        }
        if (!IsSafeFileNameTemplate(fileNameTemplate))
        {
            return Error(
                "destination.fileNameTemplate",
                "File name template must produce a safe Windows file name of at most 180 characters.");
        }

        if (!TryGetRequiredString(
                destination,
                "formatting",
                out var formatting) ||
            formatting is not ("compact" or "indented"))
        {
            return Error(
                "destination.formatting",
                "Formatting must be compact or indented.");
        }

        return [];
    }

    private static IReadOnlyList<ConfigVersionValidationError>
        ValidateContractReference(JsonElement destination)
    {
        if (!destination.TryGetProperty("contract", out var contract) ||
            contract.ValueKind != JsonValueKind.Object)
        {
            return Error(
                "destination.contract",
                "Contract is required.");
        }

        var allowedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "version", "reference", "schemaHash"
        };
        foreach (var property in contract.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                return Error(
                    $"destination.contract.{property.Name}",
                    "Property is not allowed for Contract Reference.");
            }
        }

        var requiredContractProperties = new[]
        {
            (Name: "id", Label: "Contract ID"),
            (Name: "version", Label: "Contract version"),
            (Name: "reference", Label: "Contract reference"),
            (Name: "schemaHash", Label: "Contract schema hash")
        };
        foreach (var property in requiredContractProperties)
        {
            if (!TryGetRequiredString(contract, property.Name, out _))
            {
                return Error(
                    $"destination.contract.{property.Name}",
                    $"{property.Label} is required.");
            }
        }

        return [];
    }

    private static IReadOnlyList<ConfigVersionValidationError>
        ValidateAllowedProperties(
            JsonElement destination,
            string kind,
            params string[] allowedPropertyNames)
    {
        var allowedProperties = allowedPropertyNames.ToHashSet(
            StringComparer.Ordinal);
        foreach (var property in destination.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                return Error(
                    $"destination.{property.Name}",
                    $"Property is not allowed for {kind} destination.");
            }
        }

        return [];
    }

    private static bool IsLogicalIdentifier(string value) =>
        value.Length <= 128 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.');

    private static bool IsSafeRelativeFolder(string value)
    {
        if (value.Length > MaximumPathPartLength ||
            value.StartsWith('/') ||
            value.StartsWith('\\') ||
            value.Contains('\\') ||
            (value.Length >= 2 &&
             char.IsAsciiLetter(value[0]) &&
             value[1] == ':'))
        {
            return false;
        }

        var segments = value.Split('/');
        return segments.All(segment =>
            segment is not ("" or "." or "..") &&
            IsSafeWindowsName(segment));
    }

    private static bool HasOnlyAllowedFileNameTokens(string value)
    {
        var index = 0;
        while (index < value.Length)
        {
            var openingBrace = value.IndexOf('{', index);
            var closingBraceBeforeOpening = value.IndexOf('}', index);
            if (closingBraceBeforeOpening >= 0 &&
                (openingBrace < 0 || closingBraceBeforeOpening < openingBrace))
            {
                return false;
            }
            if (openingBrace < 0)
            {
                return true;
            }

            var closingBrace = value.IndexOf('}', openingBrace + 1);
            if (closingBrace < 0)
            {
                return false;
            }

            var token = value[(openingBrace + 1)..closingBrace];
            if (!AllowedFileNameTokens.Contains(token))
            {
                return false;
            }

            index = closingBrace + 1;
        }

        return true;
    }

    private static bool IsSafeFileNameTemplate(string value)
    {
        if (value.Length > MaximumPathPartLength ||
            value.Any(character =>
                character < ' ' || character is '<' or '>' or ':' or '"' or
                    '|' or '?' or '*'))
        {
            return false;
        }

        var renderedSample = value;
        foreach (var token in AllowedFileNameTokens)
        {
            renderedSample = renderedSample.Replace(
                $"{{{token}}}",
                "sample",
                StringComparison.Ordinal);
        }

        return IsSafeWindowsName(renderedSample);
    }

    private static bool IsSafeWindowsName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.EndsWith('.') ||
            value.EndsWith(' ') ||
            value.Any(character =>
                character < ' ' || character is '<' or '>' or ':' or '"' or
                    '/' or '\\' or '|' or '?' or '*'))
        {
            return false;
        }

        var stem = value.Split('.')[0];
        return !ReservedWindowsNames.Contains(stem);
    }

    private static bool TryGetRequiredString(
        JsonElement parent,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static IReadOnlyList<ConfigVersionValidationError> Error(
        string path,
        string message)
    {
        return [new ConfigVersionValidationError(path, message)];
    }
}
