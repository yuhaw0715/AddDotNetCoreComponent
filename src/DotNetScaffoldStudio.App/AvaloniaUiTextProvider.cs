using System.Globalization;
using DotNetScaffoldStudio.Application;

namespace DotNetScaffoldStudio.App;

public sealed class AvaloniaUiTextProvider : IUiTextProvider
{
    public string Get(string key) =>
        Avalonia.Application.Current?.Resources[key] as string ?? key;

    public string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), arguments);
}
