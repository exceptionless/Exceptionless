namespace Exceptionless.Ingestion.Load;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args is ["--help"] or ["-h"])
        {
            LoadOptions.WriteUsage(Console.Out);
            return 0;
        }

        LoadOptions options;
        try
        {
            options = LoadOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            LoadOptions.WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            return await new IngestionLoadRunner(options).RunAsync();
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine($"The run exceeded the {options.Timeout:c} per-run timeout.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
