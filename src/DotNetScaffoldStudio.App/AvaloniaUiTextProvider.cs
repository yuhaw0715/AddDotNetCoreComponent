using System.Globalization;
using DotNetScaffoldStudio.Application;

namespace DotNetScaffoldStudio.App;

public sealed class AvaloniaUiTextProvider : IUiTextProvider
{
    private readonly Avalonia.Application? _application;

    public AvaloniaUiTextProvider(Avalonia.Application? application = null) => _application = application;

    public string Get(string key)
    {
        var application = _application ?? Avalonia.Application.Current;
        if (application is not null && TryGetResource(application.Resources, key, out var resource) &&
            resource is string text)
        {
            return text;
        }

        return key;
    }

    public string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), arguments);

    private static bool TryGetResource(
        Avalonia.Controls.IResourceDictionary dictionary,
        object key,
        out object? resource)
    {
        if (dictionary is Avalonia.Controls.IResourceNode resourceNode &&
            resourceNode.TryGetResource(key, null, out resource))
        {
            return true;
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries.Reverse())
        {
            if (mergedDictionary is Avalonia.Controls.IResourceNode mergedResourceNode &&
                mergedResourceNode.TryGetResource(key, null, out resource))
            {
                return true;
            }

            if (mergedDictionary is Avalonia.Controls.IResourceDictionary childDictionary &&
                TryGetResource(childDictionary, key, out resource))
            {
                return true;
            }
        }

        resource = null;
        return false;
    }
}
