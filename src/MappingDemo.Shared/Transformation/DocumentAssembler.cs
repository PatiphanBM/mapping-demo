namespace MappingDemo.Shared.Transformation;

internal sealed class DocumentAssembler
{
    private readonly SortedDictionary<string, object?> _root = new(StringComparer.Ordinal);

    public void Set(IReadOnlyList<TargetPathToken> tokens, object? value)
    {
        if (tokens.Count == 0 || tokens[0] is not PropertyPathToken)
        {
            throw new InvalidOperationException("Target paths must begin with a property.");
        }

        object current = _root;
        for (var index = 0; index < tokens.Count; index++)
        {
            var last = index == tokens.Count - 1;
            switch (tokens[index])
            {
                case PropertyPathToken property:
                    var dictionary = (IDictionary<string, object?>)current;
                    if (last)
                    {
                        if (dictionary.ContainsKey(property.Name))
                        {
                            throw new InvalidOperationException($"Target path '{property.Name}' is assigned more than once.");
                        }
                        dictionary[property.Name] = value;
                        break;
                    }
                    if (!dictionary.TryGetValue(property.Name, out var next) || next is null)
                    {
                        next = tokens[index + 1] is IndexPathToken
                            ? new List<object?>()
                            : new SortedDictionary<string, object?>(StringComparer.Ordinal);
                        dictionary[property.Name] = next;
                    }
                    current = next;
                    break;
                case IndexPathToken arrayIndex:
                    var list = (IList<object?>)current;
                    while (list.Count <= arrayIndex.Index) list.Add(null);
                    if (last)
                    {
                        if (list[arrayIndex.Index] is not null)
                        {
                            throw new InvalidOperationException($"Array index '{arrayIndex.Index}' is assigned more than once.");
                        }
                        list[arrayIndex.Index] = value;
                        break;
                    }
                    if (list[arrayIndex.Index] is null)
                    {
                        list[arrayIndex.Index] = tokens[index + 1] is IndexPathToken
                            ? new List<object?>()
                            : new SortedDictionary<string, object?>(StringComparer.Ordinal);
                    }
                    current = list[arrayIndex.Index]!;
                    break;
            }
        }
    }

    public IReadOnlyDictionary<string, object?> Build() => _root;
}
