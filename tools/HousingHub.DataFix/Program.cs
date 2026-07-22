using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;

namespace HousingHub.DataFix;

/// <summary>
/// One-off repair for rows written while the frontend was sending enum values that
/// didn't match the backend.
///
/// Two separate defects:
///
/// 1. Registration sent 1 for "Buyer/Renter" and 2 for "Homeowner", but the backend
///    enum is HouseOwner=1, Agent=2, Customer=4. Every renter was stored as a
///    HouseOwner, every homeowner as an Agent, and nobody was ever a Customer.
///
/// 2. The Add Property form sent the *array index* of the type button (0-4) rather
///    than the enum value, so "House" was saved as 0 - not a valid PropertyType at
///    all - and "Sale" was saved as 2, which the backend reads as Lease.
///
/// Dry run by default: nothing is written unless --apply is passed.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        var apply = args.Contains("--apply");
        var doCustomers = args.Contains("--customers") || !args.Contains("--properties");
        var doProperties = args.Contains("--properties") || !args.Contains("--customers");
        var region = GetOption(args, "--region") ?? "af-south-1";

        // Rows created after this point were written by the fixed frontend and must
        // not be touched. Defaults to now, so run this BEFORE deploying the FE fix,
        // or pass the deploy time explicitly.
        var cutoff = DateTime.TryParse(GetOption(args, "--created-before"), out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;

        Console.WriteLine($"Mode      : {(apply ? "APPLY (writes)" : "DRY RUN (no writes)")}");
        Console.WriteLine($"Region    : {region}");
        Console.WriteLine($"Cutoff    : rows created before {cutoff:u}");
        Console.WriteLine();

        using var client = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(region));
        var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => client).Build();

        var properties = await ScanAll<Property>(context);

        if (doCustomers)
            await FixCustomers(context, properties, cutoff, apply);

        if (doProperties)
            await FixProperties(context, properties, cutoff, apply);

        if (!apply)
            Console.WriteLine("\nNothing was written. Re-run with --apply once the plan above looks right.");

        return 0;
    }

    // ── Customers ────────────────────────────────────────────────────────

    private static async Task FixCustomers(IDynamoDBContext context, List<Property> properties,
        DateTime cutoff, bool apply)
    {
        Console.WriteLine("── Customers ──────────────────────────────────────");

        var customers = await ScanAll<Customer>(context);
        var ownerIds = properties.Select(p => p.OwnerId).ToHashSet();

        int changed = 0, skipped = 0;

        foreach (var customer in customers.OrderBy(c => c.DateCreated))
        {
            if (customer.DateCreated >= cutoff) { skipped++; continue; }

            var current = customer.CustomerType;
            var ownsProperty = ownerIds.Contains(customer.Id);
            var proposed = ProposeCustomerType(current, ownsProperty);

            if (proposed == current) { skipped++; continue; }

            var reason = ownsProperty ? "owns >=1 property" : "owns no property";
            Console.WriteLine($"  {customer.Email,-38} {current,-10} -> {proposed,-10} ({reason})");

            if (apply)
            {
                customer.CustomerType = proposed;
                await context.SaveAsync(customer);
            }
            changed++;
        }

        Console.WriteLine($"  {changed} to change, {skipped} unchanged.\n");
    }

    /// <summary>
    /// Registration's 1/2 were the form's own codes, not backend enum values.
    /// Property ownership is the tiebreaker: someone stored as HouseOwner who has
    /// never listed anything was almost certainly a renter picking "Buyer/Renter".
    /// Values the buggy form could never produce (Customer, Admin, Developer, Unset)
    /// are left alone.
    /// </summary>
    private static CustomerType ProposeCustomerType(CustomerType current, bool ownsProperty) => current switch
    {
        // Sent as "Buyer/Renter" unless they've actually listed something.
        CustomerType.HouseOwner when !ownsProperty => CustomerType.Customer,

        // Sent as "Homeowner"; the form never offered Agent.
        CustomerType.Agent => CustomerType.HouseOwner,

        _ => current
    };

    // ── Properties ───────────────────────────────────────────────────────

    private static async Task FixProperties(IDynamoDBContext context, List<Property> properties,
        DateTime cutoff, bool apply)
    {
        Console.WriteLine("── Properties ─────────────────────────────────────");

        int changed = 0, skipped = 0;

        foreach (var property in properties.OrderBy(p => p.DateCreated))
        {
            if (property.DateCreated >= cutoff) { skipped++; continue; }

            var newType = ProposePropertyType(property.PropertyType);
            var newLease = ProposeLeaseType(property.PropertyLeaseType);

            if (newType == property.PropertyType && newLease == property.PropertyLeaseType)
            {
                skipped++;
                continue;
            }

            var title = property.Title.Length > 30 ? property.Title[..30] : property.Title;
            Console.WriteLine($"  {title,-32} type {(int)property.PropertyType}->{newType}  lease {(int)property.PropertyLeaseType}->{newLease}");

            if (apply)
            {
                property.PropertyType = newType;
                property.PropertyLeaseType = newLease;
                await context.SaveAsync(property);
            }
            changed++;
        }

        Console.WriteLine($"  {changed} to change, {skipped} unchanged.\n");
    }

    /// <summary>
    /// The form submitted the index of ["House","Apartment","Guesthouse","Flat","Duplex"].
    /// Guesthouse and Flat have no backend equivalent, so they map to their nearest
    /// match (House and Apartment) - review these before applying.
    /// </summary>
    private static PropertyType ProposePropertyType(PropertyType stored) => (int)stored switch
    {
        0 => PropertyType.House,      // index 0 = "House"; 0 isn't a valid enum value
        2 => PropertyType.House,      // index 2 = "Guesthouse" -> nearest match
        3 => PropertyType.Apartment,  // index 3 = "Flat" -> nearest match
        _ => stored                   // 1 = "Apartment" and 4 = "Duplex" already line up
    };

    /// <summary>The form only offered Rent and Sale; Rent already matches.</summary>
    private static PropertyLeaseType ProposeLeaseType(PropertyLeaseType stored) =>
        (int)stored == 2 ? PropertyLeaseType.Sale : stored;

    // ── Helpers ──────────────────────────────────────────────────────────

    private static async Task<List<T>> ScanAll<T>(IDynamoDBContext context)
    {
        var search = context.ScanAsync<T>(new List<ScanCondition>());
        return await search.GetRemainingAsync();
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void PrintUsage() => Console.WriteLine("""
        Repairs customer and property enum values written by the old frontend.

          dotnet run --project tools/HousingHub.DataFix -- [options]

        Options:
          --apply                    Write changes. Omit for a dry run (default).
          --customers                Only fix customers.
          --properties               Only fix properties.
          --region <name>            AWS region (default af-south-1).
          --created-before <date>    Only touch rows created before this UTC time
                                     (default: now). Use the frontend deploy time if
                                     you run this after shipping the FE fix.

        Credentials come from the standard AWS chain (env vars, profile, or role).
        Take a table backup / PITR snapshot before using --apply.
        """);
}
