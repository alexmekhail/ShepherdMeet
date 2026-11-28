using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using MinimalApiProject.Models;
using System.Globalization;
using System.Security.Claims;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// The hosting platform sets PORT at runtime; Kestrel needs to bind to it explicitly.
var port = builder.Configuration["PORT"] ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.TrimEntries)
    ?? new[] { "http://localhost:3000" };

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder.WithOrigins(allowedOrigins)
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(BuildPostgresConnectionString(builder.Configuration.GetConnectionString("DefaultConnection"))));

// Persist DataProtection keys (which encrypt/sign the auth cookie) to
// Postgres instead of the default per-instance ephemeral storage — without
// this, a cookie issued by one container instance fails to validate on
// another once the platform scales out or recycles an instance.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("ShepherdMeet");

// Neon (and most managed Postgres providers) hand out a postgres:// URI;
// Npgsql wants a key=value connection string, so convert when needed.
// Pool is kept small since each container instance keeps its own pool and
// Neon's pooled (pgbouncer) endpoint has a limited number of slots to hand out;
// MinPoolSize=0 lets an idle/cold-started instance hold zero connections.
static string BuildPostgresConnectionString(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        throw new InvalidOperationException("No database connection string configured. Set ConnectionStrings__DefaultConnection.");

    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return raw;

    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);
    var csBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        SslMode = SslMode.Require,
        Pooling = true,
        MinPoolSize = 0,
        MaxPoolSize = 10,
    };
    return csBuilder.ConnectionString;
}

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var demoEnabled = builder.Configuration.GetValue("Demo:Enabled", builder.Environment.IsDevelopment());
var googleConfigured = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    if (googleConfigured)
        options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Events.OnValidatePrincipal = async context =>
    {
        var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
        if (email is "demo.admin@shepherdmeet.app" or "demo.parishioner@shepherdmeet.app"
            && (!demoEnabled || context.Principal?.HasClaim("shepherdmeet_demo", "true") != true))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    };
    if (builder.Environment.IsDevelopment())
    {
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    }
    else
    {
        // Frontend and API are routed through the same Vercel domain
        // (services + a path rewrite), so the auth cookie is first-party.
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
});

if (googleConfigured)
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;
        // Public path is /api/signin-google (the backend service has no
        // path-stripping transform applied to it — Vercel's container
        // runtime forwards the request path as-is), so the callback and the
        // Google Cloud Console redirect URI must both use the /api prefix.
        options.CallbackPath = "/api/signin-google";
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });
}

builder.Services.AddAuthorization();

var app = builder.Build();
var scheduling = new Scheduling(app.Configuration["Scheduling:TimeZone"] ?? "America/Los_Angeles");


// Migrations are a separate, explicit step (never run on normal container
// startup, so a cold start never blocks on DB schema work): run with
//   dotnet ShepherdMeet.dll --migrate
// against the target ConnectionStrings__DefaultConnection.
if (args.Contains("--migrate"))
{
    using var migrateScope = app.Services.CreateScope();
    var db = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    Console.WriteLine("Migrations applied.");
    return;
}

// Seed random availability slots through end of year if none exist.
// A seeding failure must never crash-loop the app, so it is best-effort.
if (demoEnabled)
using (var seedScope = app.Services.CreateScope())
{
    var seedLogger = seedScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

  try
  {
    using var transaction = Scheduling.LockAsync(seedDb, CancellationToken.None).GetAwaiter().GetResult();
    if (!seedDb.PriestAvailabilities.Any())
    {
        var rng = new Random(42);
        var today = scheduling.Today;
        var endOfYear = new DateTime(today.Year, 12, 31);
        var slots = new List<PriestAvailabilityInput>();

        for (var date = today.AddDays(1); date <= endOfYear; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                continue;

            // ~65% of weekdays have any availability
            if (rng.NextDouble() > 0.65)
                continue;

            // Two possible windows: morning (9-12) and afternoon (14-17)
            var windows = new[] { (9, 12), (14, 17) };
            foreach (var (wStart, wEnd) in windows)
            {
                // ~55% chance to include each window
                if (rng.NextDouble() > 0.55)
                    continue;

                for (var hour = wStart; hour < wEnd; hour++)
                {
                    foreach (var min in new[] { 0, 30 })
                    {
                        // ~75% chance each individual 30-min slot is available
                        if (rng.NextDouble() > 0.75)
                            continue;

                        var slotStart = date.AddHours(hour).AddMinutes(min);
                        var slotEnd = slotStart.AddMinutes(30);

                        slots.Add(new PriestAvailabilityInput
                        {
                            UserID = 1,
                            StartDate = slotStart,
                            EndDate = slotEnd,
                            Days = new List<string> { date.DayOfWeek.ToString() },
                            StartTime = TimeSpan.FromHours(hour).Add(TimeSpan.FromMinutes(min)),
                            EndTime = TimeSpan.FromHours(hour).Add(TimeSpan.FromMinutes(min + 30)),
                            IsAvailable = true
                        });
                    }
                }
            }
        }

        seedDb.PriestAvailabilities.AddRange(slots);
        seedDb.SaveChanges();
        Console.WriteLine($"Seeded {slots.Count} availability slots through {endOfYear:yyyy-MM-dd}.");
    }
    transaction.Commit();
  }
  catch (Exception ex)
  {
    seedLogger.LogError(ex, "Availability seeding failed; continuing startup without seed data.");
  }
}

// Seed two demo accounts (parishioner + admin) so recruiters can click
// through the whole app via the demo-login buttons without a Google account.
if (demoEnabled)
using (var demoScope = app.Services.CreateScope())
{
    var demoLogger = demoScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var demoDb = demoScope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        await using var transaction = await Scheduling.LockAsync(demoDb, CancellationToken.None);
        async Task<User> EnsureDemoUserAsync(string email, string firstName, string lastName)
        {
            var existing = await demoDb.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existing != null) return existing;

            var created = new User { Email = email, FirstName = firstName, LastName = lastName };
            demoDb.Users.Add(created);
            await demoDb.SaveChangesAsync();
            return created;
        }

        var demoParishioner = await EnsureDemoUserAsync("demo.parishioner@shepherdmeet.app", "Demo", "Parishioner");
        await EnsureDemoUserAsync("demo.admin@shepherdmeet.app", "Demo", "Admin (Priest)");

        if (!demoDb.Appointments.Any(a => a.UserID == demoParishioner.ID))
        {
            var today = scheduling.Today;
            demoDb.Appointments.AddRange(
                new Appointment
                {
                    UserID = demoParishioner.ID,
                    Name = "Demo Parishioner",
                    Location = "Church Office",
                    Date = today.AddDays(5).ToString("yyyy-MM-dd"),
                    Time = "10:00 AM",
                },
                new Appointment
                {
                    UserID = demoParishioner.ID,
                    Name = "Demo Parishioner",
                    Location = "Church Office",
                    Date = today.AddDays(-10).ToString("yyyy-MM-dd"),
                    Time = "2:30 PM",
                });
            await demoDb.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        demoLogger.LogError(ex, "Demo account seeding failed; continuing startup without demo accounts.");
    }
}

var frontendUrl = app.Configuration["FrontendUrl"] ?? "http://localhost:3000";

// Schedule and account management have independent allowlists.
var adminEmails = (app.Configuration["AdminEmails"] ?? "")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

var accountAdminEmails = (app.Configuration["AccountAdminEmails"] ?? "")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

bool CanManageAccounts(HttpContext context)
{
    var email = context.User.FindFirstValue(ClaimTypes.Email);
    return context.User.Identity?.IsAuthenticated == true
        && !context.User.HasClaim("shepherdmeet_demo", "true")
        && email != null
        && accountAdminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
}

bool IsAdmin(HttpContext ctx)
{
    if (ctx.User.Identity?.IsAuthenticated != true) return false;
    var email = ctx.User.FindFirstValue(ClaimTypes.Email);
    if (ctx.User.HasClaim("shepherdmeet_demo", "true"))
        return demoEnabled && email == "demo.admin@shepherdmeet.app";
    return email != null && adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
}

// Trust the HTTPS reverse proxy in front of the app (the hosting platform).
// KnownNetworks/KnownProxies must be cleared so ASP.NET accepts
// X-Forwarded-Proto from the platform's dynamic proxy IPs.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Configure the HTTP request pipeline.
app.UseCors("AllowSpecificOrigin");
app.Use(async (context, next) =>
{
    var request = context.Request;
    if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method) && request.Headers.TryGetValue("Origin", out var origin)
        && !allowedOrigins.Contains(origin.ToString(), StringComparer.OrdinalIgnoreCase)
        && !string.Equals(frontendUrl.TrimEnd('/'), origin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();

// Vercel's rewrite sends every public /api/* request to this service as-is
// (no path-stripping for container-runtime services), so every route below
// is grouped under /api to match what the browser actually requests.
var api = app.MapGroup("/api");

api.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        return Results.Ok(new { status = "healthy" });
    }
    catch (Exception)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

api.MapPost("/auth/demo-login", async (HttpContext context, AppDbContext db, DemoLoginRequest? body) =>
{
    if (!demoEnabled) return Results.NotFound();
    var email = string.Equals(body?.Role, "admin", StringComparison.OrdinalIgnoreCase)
        ? "demo.admin@shepherdmeet.app"
        : "demo.parishioner@shepherdmeet.app";

    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user == null) return Results.Problem("Demo account is not seeded yet.", statusCode: StatusCodes.Status503ServiceUnavailable);

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Email, user.Email!),
        new Claim("shepherdmeet_demo", "true"),
        new Claim(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
        new Claim(ClaimTypes.Surname, user.LastName ?? string.Empty),
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Ok();
});

api.MapGet("/priestavailabilities", async (AppDbContext db, CancellationToken cancellation) =>
{
    var today = scheduling.Today;
    var earliest = Scheduling.DateKey(today.AddDays(-1));
    var slots = await db.PriestAvailabilities
        .Where(a => a.StartDate >= today || (!a.IsAvailable && a.EndDate > today))
        .OrderBy(a => a.StartDate).ThenBy(a => a.ID).ToListAsync(cancellation);
    var appointments = await db.Appointments.Where(a => string.Compare(a.Date, earliest) >= 0)
        .Select(a => new { a.Date, a.Time, a.StartUtc, a.EndUtc }).ToListAsync(cancellation);
    var byDate = appointments.ToLookup(a => a.Date);
    var blocked = slots.Where(a => !a.IsAvailable).ToLookup(a => a.StartDate.Date);
    var now = DateTime.UtcNow;

    return Results.Ok(slots.Where(a => a.StartDate >= today)
        .GroupBy(a => new { a.StartDate, a.EndDate }).Select(group =>
    {
        var slot = group.First();
        var date = Scheduling.DateKey(slot.StartDate);
        var utc = scheduling.ToUtc(slot.StartDate, slot.EndDate);
        var nearbyDates = Scheduling.OverlappingDates(slot.StartDate, slot.EndDate);
        var booked = utc is { } interval && nearbyDates.SelectMany(day => byDate[Scheduling.DateKey(day)])
            .Any(a => a.StartUtc is { } bookedStart
                ? bookedStart < interval.End && a.EndUtc > interval.Start
                : Scheduling.OverlapsLegacy(a.Date, a.Time, slot.StartDate, slot.EndDate));
        return new
        {
            id = slot.ID,
            startDate = date,
            startTime = slot.StartDate.ToString("HH:mm", CultureInfo.InvariantCulture),
            endTime = slot.EndDate.ToString("HH:mm", CultureInfo.InvariantCulture),
            IsAvailable = utc is { } valid && valid.Start > now && group.All(a => a.IsAvailable)
                && !nearbyDates.SelectMany(day => blocked[day])
                    .Any(a => a.StartDate < slot.EndDate && a.EndDate > slot.StartDate),
            IsBooked = booked
        };
    }));
});

api.MapPost("/priestavailabilities", async (AvailabilityRequest request, HttpContext context, AppDbContext db, CancellationToken cancellation) =>
{
    if (!IsAdmin(context)) return AdminRequired(context);

    var days = request.Days?.Distinct().ToArray() ?? [];
    if (request.StartDate == default || request.EndDate < request.StartDate
        || request.EndDate.DayNumber - request.StartDate.DayNumber > 365
        || request.EndDate.Year >= 9999 || days.Length == 0
        || days.Any(day => !Enum.GetNames<DayOfWeek>().Contains(day))
        || request.StartTime < TimeSpan.Zero || request.EndTime > TimeSpan.FromDays(1)
        || request.StartTime >= request.EndTime
        || request.StartTime.Ticks % Scheduling.Duration.Ticks != 0
        || request.EndTime.Ticks % Scheduling.Duration.Ticks != 0)
        return Results.Problem("Choose valid dates, weekdays, and an increasing interval on half-hour boundaries.", statusCode: 400);

    var start = request.StartDate.ToDateTime(TimeOnly.MinValue);
    var end = request.EndDate.ToDateTime(TimeOnly.MinValue);
    var intervals = new List<(DateTime Start, DateTime End)>();
    for (var date = start; date <= end; date = date.AddDays(1))
    {
        if (!days.Contains(date.DayOfWeek.ToString())) continue;
        for (var time = request.StartTime; time < request.EndTime; time += Scheduling.Duration)
        {
            var slotStart = date + time;
            var slotEnd = slotStart + Scheduling.Duration;
            if (request.IsAvailable && scheduling.ToUtc(slotStart, slotEnd) == null)
                return Results.Problem("Open hours cannot include an ambiguous or skipped daylight-saving time.", statusCode: 400);
            intervals.Add((slotStart, slotEnd));
        }
    }

    if (intervals.Count == 0)
        return Results.Problem("The selected weekdays do not occur in this date range.", statusCode: 400);

    var user = await CurrentUser(context, db, cancellation);
    if (user == null) return Results.Unauthorized();
    await using var transaction = await Scheduling.LockAsync(db, cancellation);
    var existing = await db.PriestAvailabilities
        .Where(a => a.StartDate < end.AddDays(1) && (a.StartDate >= start || (!a.IsAvailable && a.EndDate > start)))
        .ToListAsync(cancellation);
    var byInterval = existing.ToLookup(a => (a.StartDate, a.EndDate));
    var blocked = existing.Where(a => !a.IsAvailable).ToLookup(a => a.StartDate.Date);

    if (request.IsAvailable && start == end)
    {
        var openingStart = start + request.StartTime;
        var openingEnd = start + request.EndTime;
        var legacyBlocks = existing.Where(a => !a.IsAvailable && a.StartDate < openingEnd && a.EndDate > openingStart
            && (a.EndDate - a.StartDate != Scheduling.Duration
                || a.StartDate.TimeOfDay.Ticks % Scheduling.Duration.Ticks != 0)).ToArray();

        if (legacyBlocks.Any(a => a.StartDate < start || a.EndDate > start.AddDays(1)))
            return Results.Problem("An older block crosses midnight and must be reconciled before opening these hours.",
                statusCode: 409);

        if (legacyBlocks.Any(a => a.StartDate < openingStart || a.EndDate > openingEnd))
            return Results.Problem("The opening overlaps an older block. Choose an interval that covers the whole block.",
                statusCode: 409);

        if (legacyBlocks.Length > 0)
        {
            var blockIds = legacyBlocks.Select(a => a.ID).ToArray();
            if (await db.Appointments.AnyAsync(a => a.AvailabilityID.HasValue
                && blockIds.Contains(a.AvailabilityID.Value), cancellation))
                return Results.Problem("An older block in this interval is linked to an appointment. Reconcile that appointment before reopening the interval.",
                    statusCode: 409);

            // Replace contained, unreferenced off-grid blocks with the requested half-hour slots.
            db.PriestAvailabilities.RemoveRange(legacyBlocks);
        }
    }

    foreach (var interval in intervals)
    {
        var exact = byInterval[interval];
        // A dated block takes precedence over a repeated opening. A single-date
        // opening can explicitly restore an interval previously blocked.
        if (request.IsAvailable && request.StartDate != request.EndDate
            && blocked[interval.Start.Date].Concat(blocked[interval.Start.Date.AddDays(-1)])
                .Any(a => a.StartDate < interval.End && a.EndDate > interval.Start))
            continue;
        if (exact.Any())
        {
            foreach (var slot in exact) slot.IsAvailable = request.IsAvailable;
            continue;
        }
        db.PriestAvailabilities.Add(new PriestAvailabilityInput
        {
            UserID = user.ID,
            StartDate = interval.Start,
            EndDate = interval.End,
            Days = [interval.Start.DayOfWeek.ToString()],
            StartTime = interval.Start.TimeOfDay,
            EndTime = interval.Start.TimeOfDay + Scheduling.Duration,
            IsAvailable = request.IsAvailable,
        });
    }

    await db.SaveChangesAsync(cancellation);
    await transaction.CommitAsync(cancellation);
    return Results.NoContent();
});

api.MapDelete("/priestavailabilities", async (HttpContext context, AppDbContext db, CancellationToken cancellation) =>
{
    if (!IsAdmin(context)) return AdminRequired(context);
    await using var transaction = await Scheduling.LockAsync(db, cancellation);
    var retained = db.Appointments.Where(a => a.AvailabilityID != null).Select(a => a.AvailabilityID);
    await db.PriestAvailabilities.Where(a => !retained.Contains(a.ID)).ExecuteDeleteAsync(cancellation);
    await db.PriestAvailabilities.Where(a => retained.Contains(a.ID))
        .ExecuteUpdateAsync(update => update.SetProperty(a => a.IsAvailable, false), cancellation);
    await transaction.CommitAsync(cancellation);
    return Results.NoContent();
});

api.MapDelete("/priestavailabilities/{id:int}", async (int id, HttpContext context, AppDbContext db, CancellationToken cancellation) =>
{
    if (!IsAdmin(context)) return AdminRequired(context);
    await using var transaction = await Scheduling.LockAsync(db, cancellation);
    var slot = await db.PriestAvailabilities.FindAsync([id], cancellation);
    if (slot == null) return Results.NotFound();
    if (await db.Appointments.AnyAsync(a => a.AvailabilityID == id, cancellation))
        return Results.Conflict();
    slot.IsAvailable = false;
    await db.SaveChangesAsync(cancellation);
    await transaction.CommitAsync(cancellation);
    return Results.NoContent();
});

api.MapGet("/", () => "Hello World!");

api.MapPost("/appointments", async (AppointmentRequest request, HttpContext context, AppDbContext db, CancellationToken cancellation) =>
{
    var user = await CurrentUser(context, db, cancellation);
    if (user == null) return Results.Unauthorized();
    if (request.AvailabilityId <= 0)
        return Results.Problem("Choose an appointment time from the calendar.", statusCode: 400);

    await using var transaction = await Scheduling.LockAsync(db, cancellation);
    var slot = await db.PriestAvailabilities.FindAsync([request.AvailabilityId], cancellation);
    if (slot == null || !slot.IsAvailable
        || scheduling.ToUtc(slot.StartDate, slot.EndDate) is not { } interval
        || interval.Start <= DateTime.UtcNow)
        return SlotUnavailable();

    if (await db.PriestAvailabilities.AnyAsync(a => !a.IsAvailable &&
        a.StartDate < slot.EndDate && a.EndDate > slot.StartDate, cancellation))
        return SlotUnavailable();

    var date = Scheduling.DateKey(slot.StartDate);
    var nearbyDates = Scheduling.OverlappingDates(slot.StartDate, slot.EndDate).Select(Scheduling.DateKey).ToArray();
    var overlaps = await db.Appointments.AnyAsync(a => a.StartUtc < interval.End && a.EndUtc > interval.Start, cancellation);
    var legacy = await db.Appointments.Where(a => a.StartUtc == null && nearbyDates.Contains(a.Date))
        .Select(a => new { a.Date, a.Time }).ToListAsync(cancellation);
    if (overlaps || legacy.Any(a => Scheduling.OverlapsLegacy(a.Date, a.Time, slot.StartDate, slot.EndDate)))
        return SlotUnavailable();

    var appointment = new Appointment
    {
        UserID = user.ID,
        Name = (user.FirstName + " " + user.LastName).Trim(),
        Location = Scheduling.Church,
        Date = date,
        Time = slot.StartDate.ToString("h:mm tt", CultureInfo.InvariantCulture) + " – "
            + slot.EndDate.ToString("h:mm tt", CultureInfo.InvariantCulture),
        AvailabilityID = slot.ID,
        StartUtc = interval.Start,
        EndUtc = interval.End,
        CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
    };
    db.Appointments.Add(appointment);
    try
    {
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
    }
    catch (DbUpdateException error) when (error.InnerException is PostgresException
        { ConstraintName: "EX_Appointments_Interval" or "IX_Appointments_AvailabilityID" })
    {
        return SlotUnavailable();
    }
    return Results.Created($"/api/appointments/{appointment.ID}", appointment);
});

api.MapDelete("/appointments/{id:int}", async (int id, HttpContext context, AppDbContext db, CancellationToken cancellation) =>
{
    var user = await CurrentUser(context, db, cancellation);
    if (user == null) return Results.Unauthorized();
    await using var transaction = await Scheduling.LockAsync(db, cancellation);
    var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.ID == id && a.UserID == user.ID, cancellation);
    if (appointment == null) return Results.NotFound();
    db.Appointments.Remove(appointment);
    await db.SaveChangesAsync(cancellation);
    await transaction.CommitAsync(cancellation);
    return Results.NoContent();
});

static IResult SlotUnavailable() =>
    Results.Problem("This time is no longer available. Choose another time from the calendar.", statusCode: 409);

static async Task<User?> CurrentUser(HttpContext context, AppDbContext db, CancellationToken cancellation)
{
    var email = context.User.FindFirstValue(ClaimTypes.Email);
    if (string.IsNullOrWhiteSpace(email) || context.User.Identity?.IsAuthenticated != true) return null;

    var matches = await db.Users.Where(u => u.Email == email).Take(2).ToListAsync(cancellation);
    return matches.Count == 1 ? matches[0] : null;
}

static IResult AdminRequired(HttpContext context) => context.User.Identity?.IsAuthenticated == true
    ? Results.StatusCode(StatusCodes.Status403Forbidden)
    : Results.Unauthorized();

api.MapGet("/appointments", async (HttpContext context, AppDbContext db) =>
{
    var emailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    if (string.IsNullOrEmpty(emailClaim)) return Results.Unauthorized();

    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == emailClaim);
    if (user == null) return Results.NotFound("User not found.");

    var appointments = await db.Appointments
        .Where(a => a.UserID == user.ID)
        .OrderBy(a => a.Date)
        .ThenBy(a => a.Time)
        .ToListAsync();
    return Results.Ok(appointments);
});

api.MapGet("/auth/options", () => Results.Ok(new { demoEnabled, googleConfigured }));

api.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

// Remaining endpoints (user authentication, profiles, meetings, etc.)
api.MapGet("/login", async (HttpContext context) =>
{
    if (!googleConfigured)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/api/post-login"
    });
});

api.MapGet("/post-login", async (HttpContext context, AppDbContext db) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var emailClaim = context.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
        var nameClaim = context.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
        var FirstName = context.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Value;
        var LastName = context.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname")?.Value;

        if (emailClaim != null)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailClaim);
            if (user != null)
            {
                // User exists, redirect to frontend
                return Results.Redirect(frontendUrl + "/");
            }
            else
            {
                // User does not exist, create a new account
                var newUser = new User
                {
                    FirstName = FirstName,
                    LastName = LastName,
                    Email = emailClaim
                };
                db.Users.Add(newUser);
                await db.SaveChangesAsync();

                return Results.Redirect(frontendUrl + "/");
            }
        }
    }
    return Results.Unauthorized();
});


api.MapGet("/profile", async (HttpContext context, AppDbContext db) =>
{
    var emailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    if (string.IsNullOrEmpty(emailClaim))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == emailClaim);
    if (user == null)
    {
        return Results.NotFound("User not found.");
    }

    var result = new
    {
        Id = user.ID,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        IsAdmin = IsAdmin(context),
        TimeZone = scheduling.TimeZoneId
    };

    return Results.Ok(result);
});

api.MapPost("/profile", async (HttpContext context, AppDbContext db) =>
{
    var emailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    if (string.IsNullOrEmpty(emailClaim))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == emailClaim);
    if (user == null)
    {
        return Results.NotFound("User not found.");
    }

    var result = new
    {
        Id = user.ID,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        IsAdmin = IsAdmin(context),
        TimeZone = scheduling.TimeZoneId
    };

    return Results.Ok(result);
});




api.MapGet("/users", async (HttpContext context, AppDbContext db) =>
{
    if (!CanManageAccounts(context)) return AdminRequired(context);

    var users = await db.Users.ToListAsync();
    return Results.Ok(users);
});

api.MapPut("/users/{id}", async (int id, HttpContext context, User updatedUser, AppDbContext db) =>
{
    if (!CanManageAccounts(context)) return AdminRequired(context);

    var user = await db.Users.FindAsync(id);
    if (user == null)
    {
        return Results.NotFound();
    }

    user.FirstName = updatedUser.FirstName;
    user.Email = updatedUser.Email;
    // Update other fields as necessary

    await db.SaveChangesAsync();
    return Results.Ok(user);
});

api.MapPost("/users/cancel", (HttpContext context) =>
{
    if (!CanManageAccounts(context)) return AdminRequired(context);

    // Logic to handle cancel operation
    return Results.Ok("Operation canceled.");
});

app.Run();

record AppointmentRequest(int AvailabilityId);
record AvailabilityRequest(DateOnly StartDate, DateOnly EndDate, string[]? Days,
    TimeSpan StartTime, TimeSpan EndTime, bool IsAvailable);
record DemoLoginRequest(string? Role);
