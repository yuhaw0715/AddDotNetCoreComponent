namespace DotNetScaffoldStudio.CommandProbe;

public static class ProbeMarker;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args is ["mixed"])
        {
            for (var index = 0; index < 4096; index++)
            {
                Console.Out.WriteLine($"OUT {index:D4} {new string('o', 192)}");
                Console.Error.WriteLine($"ERR {index:D4} {new string('e', 192)}");
            }

            return 23;
        }

        if (args is ["secret", var secret])
        {
            Console.Out.WriteLine($"stdout={secret}");
            Console.Error.WriteLine($"stderr={secret}");
            return 0;
        }

        if (args is ["wait"])
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }

        return 0;
    }
}
