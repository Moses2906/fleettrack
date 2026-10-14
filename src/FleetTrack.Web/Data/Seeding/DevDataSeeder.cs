using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Data.Seeding;

/// <summary>
/// Fills an empty development database with fictional demo data. Every company, person,
/// ID number and registration number below is made up.
/// Dates are calculated relative to today so the demo always shows a mix of
/// expired, due-soon and valid licence disks.
/// </summary>
public static class DevDataSeeder
{
    private const string SeedUser = "seed@fleettrack.local";

    private static readonly string[] Workshops =
    [
        "Bosveld Auto Repairs", "Midrand Fleet Services", "Tyre Corner Kempton Park",
        "City Diesel Workshop", "Coastal Commercial Motors", "Brakes & Clutch Express"
    ];

    private static readonly string[] Processors = ["N. Govender", "S. Smit", "T. Mabuza"];

    public static async Task SeedAsync(ApplicationDbContext db, DateOnly today)
    {
        if (await db.Vehicles.AnyAsync())
            return;

        // Demo data isn't something a person did, so it isn't written to the audit log.
        db.AuditingEnabled = false;

        // A fixed seed makes the "random" costs and odometer readings the same on every run.
        var random = new Random(2026);

        var holders = new List<LicenceHolder>
        {
            new() { HolderType = HolderType.Company, Name = "Karoo Line Logistics (Pty) Ltd", Identifier = "2014/208815/07" },
            new() { HolderType = HolderType.Company, Name = "Highveld Courier Services CC", Identifier = "2009/045512/23" },
            new() { HolderType = HolderType.Company, Name = "Umhlanga Fresh Produce (Pty) Ltd", Identifier = "2017/331904/07" },
            new() { HolderType = HolderType.Company, Name = "Protea Plant & Tool Hire (Pty) Ltd", Identifier = "2012/118276/07" },
            new() { HolderType = HolderType.Individual, Name = "Thabo Mokoena", Identifier = "8504125123086" },
            new() { HolderType = HolderType.Individual, Name = "Anele van Wyk", Identifier = "9207300154087" },
            new() { HolderType = HolderType.Individual, Name = "Pieter Joubert", Identifier = "7801196340081" },
        };

        string[] driverNames =
        [
            "Sipho Ndlovu", "Johan Botha", "Lerato Dlamini", "Ayesha Patel", "Mandla Khumalo",
            "Riaan Pretorius", "Nomvula Zulu", "Kagiso Molefe", "Fatima Isaacs", "Bongani Mahlangu"
        ];
        var drivers = driverNames.Select((name, i) => new Driver
        {
            FullName = name,
            Phone = $"082 555 01{i:D2}",
            Email = $"{name.Split(' ')[0].ToLowerInvariant()}@example.com",
            DriverLicenceNumber = $"40{random.Next(10_000_000, 99_999_999)}{(char)('A' + i)}Z",
        }).ToList();

        db.LicenceHolders.AddRange(holders);
        db.Drivers.AddRange(drivers);

        // (registration, make, model, year, categoryId, holder, driver or -1, expiry offset in days, missing documents)
        var specs = new (string Reg, string Make, string Model, int Year, int Cat, int Holder, int Driver, int ExpiryOffset, DocumentType[] Missing)[]
        {
            ("BC 34 FG GP", "Toyota", "Hilux 2.4 GD-6", 2021, 2, 0, 0, 212, []),
            ("CA 482-913", "Volkswagen", "Polo Vivo 1.4", 2020, 1, 1, 1, -45, []),
            ("CY 51207", "Nissan", "NP200 1.6", 2019, 2, 1, 2, 9, []),
            ("DF 72 KS GP", "Isuzu", "D-Max 1.9 Ddi", 2022, 2, 0, 3, 140, [DocumentType.ProxyId]),
            ("HJR 418 MP", "Toyota", "Quantum 2.5 D-4D", 2018, 3, 2, 4, 27, []),
            ("ND 304-118", "Ford", "Ranger 2.0 XL", 2023, 2, 2, 5, 301, []),
            ("KL 09 TN GP", "Mercedes-Benz", "Actros 2645", 2017, 6, 0, 6, -3, []),
            ("CJ 71-552", "Hyundai", "H100 2.6", 2016, 2, 3, -1, 66, [DocumentType.Brnc, DocumentType.ProxyLetter]),
            ("FX 28 PL GP", "Isuzu", "NPR 400", 2020, 5, 0, 7, 18, []),
            ("BNM 552 NW", "Toyota", "Corolla Cross 1.8", 2023, 1, 4, -1, 188, []),
            ("CF 210-447", "Honda", "XR150L", 2022, 7, 1, 8, 2, []),
            ("MT 63 WR GP", "UD Trucks", "Quon GW 26.460", 2019, 6, 0, 9, 95, [DocumentType.Rc1]),
            ("NU 77821", "Toyota", "HiAce Ses'fikile", 2021, 3, 2, -1, -12, []),
            ("CL 88-301", "Suzuki", "Swift 1.2 GL", 2024, 1, 5, -1, 254, []),
            ("GZD 731 L", "Mahindra", "Pik Up 2.2 S4", 2018, 2, 6, -1, 41, []),
            ("RS 15 VB GP", "Hino", "300 815", 2021, 5, 3, -1, 330, [DocumentType.Brnc, DocumentType.ProxyLetter, DocumentType.ProxyId]),
            ("CAW 39-062", "Volkswagen", "Crafter 50 2.0 TDI", 2019, 2, 1, -1, 120, []),
            ("FLEET 1-WP", "Toyota", "Fortuner 2.8 GD-6", 2022, 1, 0, -1, 167, []),
            ("PW 47 HD GP", "Volvo", "B8R Coach", 2018, 4, 2, -1, 59, [DocumentType.ProxyLetter]),
            ("CA 905-276", "Afrit", "Tri-axle Flat Deck", 2015, 8, 3, -1, 276, []),
        };

        foreach (var s in specs)
        {
            var vehicle = new Vehicle
            {
                RegistrationNumber = s.Reg,
                Make = s.Make,
                Model = s.Model,
                Year = s.Year,
                CategoryId = s.Cat,
                LicenceHolder = holders[s.Holder],
                CurrentDriver = s.Driver >= 0 ? drivers[s.Driver] : null,
                LicenceExpiryDate = today.AddDays(s.ExpiryOffset),
                HasRc1 = true,
                HasBrnc = true,
                HasProxyLetter = true,
                HasProxyId = true,
            };
            foreach (var missing in s.Missing)
                vehicle.SetDocumentHeld(missing, false);

            AddRenewalHistory(vehicle, s.Cat, today, random);
            AddServiceHistory(vehicle, s.Cat, today, random);

            db.Vehicles.Add(vehicle);
        }

        await db.SaveChangesAsync();
        db.AuditingEnabled = true;
    }

    /// <summary>One renewal per year, working backwards from the current disk, for the last five years.</summary>
    private static void AddRenewalHistory(Vehicle vehicle, int categoryId, DateOnly today, Random random)
    {
        decimal baseFee = categoryId switch
        {
            1 => 650m, 2 => 900m, 3 => 1_150m, 4 => 3_800m,
            5 => 2_400m, 6 => 6_200m, 7 => 350m, _ => 500m
        };
        var fiveYearsAgo = today.AddYears(-5);
        var expiry = vehicle.LicenceExpiryDate;

        for (var yearsBack = 0; ; yearsBack++)
        {
            var previousExpiry = expiry.AddYears(-1);
            var purchased = previousExpiry.AddDays(-random.Next(0, 25));
            if (purchased < fiveYearsAgo || purchased.Year < (vehicle.Year ?? 2000))
                break;

            var renewal = new LicenceRenewal
            {
                DatePurchased = purchased,
                PreviousExpiryDate = previousExpiry,
                NewExpiryDate = expiry,
                // Roughly 6% fee increase per year.
                AmountPaid = Math.Round(baseFee / (decimal)Math.Pow(1.06, yearsBack), 2),
                ReferenceNumber = $"LR{purchased.Year}{random.Next(100_000, 999_999)}",
                ProcessedBy = Processors[random.Next(Processors.Length)],
                CreatedAtUtc = purchased.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
                CreatedBy = SeedUser,
            };

            // The most recent renewal on a few vehicles is still "in progress" (no copy filed yet).
            var stepsToRecord = yearsBack == 0 && random.Next(4) == 0 ? 2 : 3;
            var steps = new[]
            {
                (RenewalStepType.DiskHandedToDriver, purchased.AddDays(random.Next(1, 3))),
                (RenewalStepType.DiskFittedToVehicle, purchased.AddDays(random.Next(3, 5))),
                (RenewalStepType.CopyFiled, purchased.AddDays(random.Next(5, 9))),
            };
            foreach (var (type, date) in steps.Take(stepsToRecord).Where(step => step.Item2 <= today))
            {
                renewal.Steps.Add(new RenewalStep { StepType = type, StepDate = date, RecordedBy = SeedUser });
            }

            vehicle.LicenceRenewals.Add(renewal);
            expiry = previousExpiry;
        }
    }

    /// <summary>A service or repair roughly every 4-7 months over the last five years.</summary>
    private static void AddServiceHistory(Vehicle vehicle, int categoryId, DateOnly today, Random random)
    {
        var isHeavy = categoryId is 4 or 5 or 6;
        var kmPerMonth = isHeavy ? random.Next(5_000, 8_000) : random.Next(1_200, 2_800);

        var start = today.AddYears(-5);
        var firstPossible = new DateOnly(vehicle.Year ?? start.Year, 3, 1);
        var date = (firstPossible > start ? firstPossible : start).AddDays(random.Next(10, 90));
        var odometer = Math.Max(0, (date.Year - (vehicle.Year ?? date.Year)) * 12 * kmPerMonth) + random.Next(500, 5_000);

        ServiceRecord? latest = null;
        while (date <= today)
        {
            var (workType, description, cost) = PickWork(categoryId, random);
            latest = new ServiceRecord
            {
                ServiceDate = date,
                OdometerKm = odometer,
                WorkType = workType,
                Description = description,
                Workshop = Workshops[random.Next(Workshops.Length)],
                Cost = cost,
                CreatedAtUtc = date.ToDateTime(new TimeOnly(14, 0), DateTimeKind.Utc),
                CreatedBy = SeedUser,
            };
            vehicle.ServiceRecords.Add(latest);

            var monthsUntilNext = random.Next(4, 8);
            date = date.AddMonths(monthsUntilNext).AddDays(random.Next(-10, 10));
            odometer += monthsUntilNext * kmPerMonth + random.Next(-800, 800);
        }

        // Only the latest service carries a "next service due" reminder; some are already overdue.
        if (latest is not null)
            latest.NextServiceDueDate = latest.ServiceDate.AddMonths(6);
    }

    private static (WorkType, string, decimal) PickWork(int categoryId, Random random)
    {
        var multiplier = categoryId switch { 4 or 6 => 3.2m, 5 => 2.0m, 7 => 0.4m, 8 => 0.5m, _ => 1m };
        var roll = random.Next(100);
        var (type, description, baseCost) = roll switch
        {
            < 60 => (WorkType.RoutineService, "Minor service: oil, oil filter and safety check", 2_400m),
            < 70 => (WorkType.Tyres, "Replaced two front tyres and wheel alignment", 4_200m),
            < 80 => (WorkType.Brakes, "Replaced front brake pads and skimmed discs", 2_900m),
            < 88 => (WorkType.Electrical, "Replaced battery and tested charging system", 2_100m),
            < 95 => (WorkType.Repair, "Replaced leaking water pump and coolant flush", 5_600m),
            _ => (WorkType.Roadworthy, "Roadworthy inspection and certificate", 750m),
        };
        var cost = Math.Round(baseCost * multiplier * (decimal)(0.85 + random.NextDouble() * 0.3), 2);
        return (type, description, cost);
    }
}
