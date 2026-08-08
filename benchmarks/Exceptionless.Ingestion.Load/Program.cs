namespace Exceptionless.Ingestion.Load;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        LoadOptions options;
        try
        {
            options = LoadOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            LoadOptions.WriteUsage();
            return 2;
        }

        try
        {
            return await new IngestionLoadRunner(options).RunAsync();
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine($"The comparison exceeded the {options.Timeout:c} per-run timeout.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
