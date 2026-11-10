namespace RoomToRentRadar.Api.Data.Seed;

public static class SeedCommand
{
    private const int DefaultCount = 100_000;
    private const int MaxCount = 1_000_000;

    public static bool IsSeedCommand(string[] args)
        => args.Length > 0 && string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> ExecuteAsync(
        AppDbContext dbContext,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseCount(args, out var count, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var added = await DatabaseSeeder.SeedBenchmarkAsync(dbContext, count, cancellationToken);
        Console.WriteLine(
            added == 0
                ? $"Database already has at least {count} listings."
                : $"Seeded {added} listings (target {count}).");

        return 0;
    }

    private static bool TryParseCount(string[] args, out int count, out string? error)
    {
        count = DefaultCount;
        error = null;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument.Equals("--count", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length
                    || !int.TryParse(args[index + 1], out count))
                {
                    error = "--count requires an integer value.";
                    return false;
                }

                index++;
            }
            else if (argument.StartsWith("--count=", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(argument["--count=".Length..], out count))
                {
                    error = "--count requires an integer value.";
                    return false;
                }
            }
            else
            {
                error = $"Unknown seed argument '{argument}'. Usage: seed [--count N]";
                return false;
            }
        }

        if (count is < 1 or > MaxCount)
        {
            error = $"--count must be between 1 and {MaxCount}.";
            return false;
        }

        return true;
    }
}
