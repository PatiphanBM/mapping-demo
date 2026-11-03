namespace MappingDemo.Shared.Transformation;

internal abstract record TargetPathToken;

internal sealed record PropertyPathToken(string Name) : TargetPathToken;

internal sealed record IndexPathToken(int Index) : TargetPathToken;

internal sealed record ResolvedTargetPath(
    string CanonicalPath,
    TargetNode Node,
    IReadOnlyList<TargetPathToken> Tokens);

internal static class TargetPath
{
    public static ResolvedTargetPath? Resolve(
        TargetSchema schema,
        string path,
        bool relativeToArrayItem = false,
        TargetNode? arrayItem = null)
    {
        if (!TryParse(path, out var tokens))
        {
            return null;
        }

        var current = relativeToArrayItem
            ? arrayItem ?? throw new ArgumentNullException(nameof(arrayItem))
            : schema.Root;
        foreach (var token in tokens)
        {
            if (token is PropertyPathToken property)
            {
                if (current.Kind != TargetNodeKind.Object)
                {
                    return null;
                }

                current = current.Properties.SingleOrDefault(node =>
                    string.Equals(node.Name, property.Name, StringComparison.Ordinal))!;
                if (current is null)
                {
                    return null;
                }
            }
            else if (token is IndexPathToken)
            {
                if (current.Kind != TargetNodeKind.Array || current.ItemSchema is null)
                {
                    return null;
                }
                current = current.ItemSchema;
            }
        }

        return new ResolvedTargetPath(path, current, tokens);
    }

    public static bool TryParse(string path, out IReadOnlyList<TargetPathToken> tokens)
    {
        var result = new List<TargetPathToken>();
        tokens = result;
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('.') || path.EndsWith('.'))
        {
            return false;
        }

        var index = 0;
        while (index < path.Length)
        {
            var start = index;
            while (index < path.Length && path[index] is not '.' and not '[')
            {
                index++;
            }
            if (index == start)
            {
                return false;
            }
            result.Add(new PropertyPathToken(path[start..index]));

            while (index < path.Length && path[index] == '[')
            {
                var close = path.IndexOf(']', index + 1);
                if (close < 0 || !int.TryParse(path[(index + 1)..close], out var value) || value < 0)
                {
                    return false;
                }
                result.Add(new IndexPathToken(value));
                index = close + 1;
            }

            if (index == path.Length) break;
            if (path[index] != '.') return false;
            index++;
        }
        return result.Count > 0;
    }

    public static bool Conflicts(
        IReadOnlyList<TargetPathToken> left,
        IReadOnlyList<TargetPathToken> right)
    {
        var common = Math.Min(left.Count, right.Count);
        for (var index = 0; index < common; index++)
        {
            if (left[index] != right[index]) return false;
        }
        return true;
    }
}
