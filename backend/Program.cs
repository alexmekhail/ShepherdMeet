using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using MinimalApiProject.Models;
using System.Text.Json;
using System.Security.Claims;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Render sets PORT at runtime; Kestrel needs to bind to it explicitly.
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

// Neon (and most managed Postgres providers) hand out a postgres:// URI;
// Npgsql wants a key=value connection string, so convert when needed.
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
    };
    return csBuilder.ConnectionString;
}

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
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
    if (builder.Environment.IsDevelopment())
    {
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    }
    else
    {
        // Frontend and API live on different Render subdomains, so the auth
        // cookie has to be sent cross-site.
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
});

if (googleConfigured)
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;
        options.CallbackPath = "/signin-google";
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });
}

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-apply migrations on startup (creates the DB if it doesn't exist).
// Retry a few times in case Neon's compute is waking up from idle suspend.
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    const int maxAttempts = 5;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            startupLogger.LogWarning(ex,
                "Migration attempt {Attempt}/{Max} failed ({Message}); retrying in 3s.",
                attempt, maxAttempts, ex.Message);
            Thread.Sleep(3000);
        }
    }
}

// Seed random availability slots through end of year if none exist.
// A seeding failure must never crash-loop the app, so it is best-effort.
using (var seedScope = app.Services.CreateScope())
{
    var seedLogger = seedScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

  try
  {
    if (!seedDb.PriestAvailabilities.Any())
    {
        var rng = new Random(42);
        var today = DateTime.Today;
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
  }
  catch (Exception ex)
  {
    seedLogger.LogError(ex, "Availability seeding failed; continuing startup without seed data.");
  }
}

// Seed two demo accounts (parishioner + admin) so recruiters can click
// through the whole app via the demo-login buttons without a Google account.
using (var demoScope = app.Services.CreateScope())
{
    var demoLogger = demoScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var demoDb = demoScope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
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
            var today = DateTime.Today;
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
    }
    catch (Exception ex)
    {
        demoLogger.LogError(ex, "Demo account seeding failed; continuing startup without demo accounts.");
    }
}

var frontendUrl = app.Configuration["FrontendUrl"] ?? "http://localhost:3000";

// Emails allowed to hit destructive/admin endpoints. Override with the
// "AdminEmails" config key (comma-separated) in production.
var adminEmails = (app.Configuration["AdminEmails"] ?? "heneinfilobatire@gmail.com,alexmekhail10@gmail.com,demo.admin@shepherdmeet.app")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

bool IsAdmin(HttpContext ctx)
{
    var email = ctx.User.Claims.FirstOrDefault(c =>
        c.Type == ClaimTypes.Email ||
        c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
    return email != null && adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
}

// Trust the HTTPS reverse proxy in front of the app (Render).
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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext db) =>
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

app.MapPost("/auth/demo-login", async (HttpContext context, AppDbContext db, DemoLoginRequest? body) =>
{
    var email = string.Equals(body?.Role, "admin", StringComparison.OrdinalIgnoreCase)
        ? "demo.admin@shepherdmeet.app"
        : "demo.parishioner@shepherdmeet.app";

    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user == null) return Results.Problem("Demo account is not seeded yet.", statusCode: StatusCodes.Status503ServiceUnavailable);

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Email, user.Email!),
        new Claim(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
        new Claim(ClaimTypes.Surname, user.LastName ?? string.Empty),
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Ok();
});

app.MapGet("/priestavailabilities", async (AppDbContext db) => {
    var availabilityList = await db.PriestAvailabilities.ToListAsync();

    // Format the response to include formatted times
    var formattedAvailabilityList = availabilityList.Select(availability => new
    {
        id = availability.ID,
        availability.UserID,
        startDate = availability.StartDate.ToString("yyyy-MM-dd"),
        endDate = availability.EndDate.ToString("yyyy-MM-dd"),
        Days = availability.Days,
        startTime = availability.StartDate.ToString("HH:mm"),
        endTime = availability.EndDate.ToString("HH:mm"),
        availability.IsAvailable
    }).ToList();

    return Results.Ok(formattedAvailabilityList);
});

app.MapPost("/priestavailabilities", async (PriestAvailabilityInput availability, AppDbContext db) => {
    // Loop through the provided days of availability
    foreach (var day in availability.Days)
    {
        // Parse the day to get the corresponding DayOfWeek enum value
        var dayOfWeek = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), day);

        // Check if the day falls between StartDate and EndDate
        var matchingDate = GetDateForDayOfWeekInRange(availability.StartDate, availability.EndDate, dayOfWeek);

        if (matchingDate == null)
        {
            // Skip creating availability if the day does not fall within the range
            Console.WriteLine($"Day {day} does not fall between {availability.StartDate:yyyy-MM-dd} and {availability.EndDate:yyyy-MM-dd}. Skipping.");
            continue;
        }

        // Loop through the times in 30-minute intervals
        for (var currentTime = availability.StartTime; currentTime < availability.EndTime; currentTime = currentTime.Add(TimeSpan.FromMinutes(30)))
        {
            // Create start and end date-times for each slot
            var slotStartDateTime = matchingDate.Value.Date.Add(currentTime);
            var slotEndDateTime = slotStartDateTime.Add(TimeSpan.FromMinutes(30));

            Console.WriteLine($"Slot start date and time: {slotStartDateTime}, Slot end date and time: {slotEndDateTime}");

            // Check if an availability for this user, day, and time slot already exists
            var existingAvailability = await db.PriestAvailabilities
                .FirstOrDefaultAsync(a => a.UserID == availability.UserID
                                          && a.StartDate == slotStartDateTime
                                          && a.EndDate == slotEndDateTime);

            if (existingAvailability != null)
            {
                // If the existing availability is false, do not allow changing to true
                if (!existingAvailability.IsAvailable && availability.IsAvailable)
                {
                    // Skip updating from false to true
                    continue;
                }

                // If the existing availability has a different status, override it
                existingAvailability.IsAvailable = availability.IsAvailable;
                db.PriestAvailabilities.Update(existingAvailability);
            }
            else
            {
                // If no existing availability, create a new one
                var slotAvailability = new PriestAvailabilityInput
                {
                    UserID = availability.UserID,
                    StartDate = slotStartDateTime,
                    EndDate = slotEndDateTime,
                    Days = new List<string> { day },
                    StartTime = currentTime,
                    EndTime = currentTime.Add(TimeSpan.FromMinutes(30)),
                    IsAvailable = availability.IsAvailable
                };

                db.PriestAvailabilities.Add(slotAvailability);
            }
        }
    }

    await db.SaveChangesAsync();
    return Results.Created($"/priestavailabilities/{availability.UserID}", availability);
});

// Helper function to find the date of a day of the week within a date range
DateTime? GetDateForDayOfWeekInRange(DateTime startDate, DateTime endDate, DayOfWeek targetDayOfWeek)
{
    // Get the first occurrence of the target day after the start date
    var daysUntilTarget = ((int)targetDayOfWeek - (int)startDate.DayOfWeek + 7) % 7;
    var targetDate = startDate.AddDays(daysUntilTarget);

    // Check if this date falls within the range of startDate and endDate
    if (targetDate <= endDate)
    {
        return targetDate;
    }

    // Return null if the day does not fall within the range
    return null;
}

app.MapDelete("/priestavailabilities", async (HttpContext context, AppDbContext db) => {
    if (!IsAdmin(context)) return Results.Unauthorized();
    db.PriestAvailabilities.RemoveRange(db.PriestAvailabilities);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/priestavailabilities/{id:int}", async (int id, AppDbContext db) => {
    var slot = await db.PriestAvailabilities.FindAsync(id);
    if (slot == null) return Results.NotFound();
    db.PriestAvailabilities.Remove(slot);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Frontend integration: Example payload for priest availability
app.MapPost("/priestavailabilities/frontend", async (HttpContext context, AppDbContext db) =>
{
    // Reading JSON data from frontend
    var availabilityData = await context.Request.ReadFromJsonAsync<PriestAvailabilityInput>();

    if (availabilityData == null)
        return Results.BadRequest("Invalid availability data.");

    var existingAvailability = await db.PriestAvailabilities.FirstOrDefaultAsync(a => a.UserID == availabilityData.UserID);

    if (existingAvailability != null)
    {
        // Update existing availability
        existingAvailability.StartDate = availabilityData.StartDate;
        existingAvailability.EndDate = availabilityData.EndDate;
        existingAvailability.Days = availabilityData.Days;
        existingAvailability.StartTime = availabilityData.StartTime;
        existingAvailability.EndTime = availabilityData.EndTime;
        db.PriestAvailabilities.Update(existingAvailability);
    }
    else
    {
        // Create new availability
        var newAvailability = new PriestAvailabilityInput
        {
            StartDate = availabilityData.StartDate,
            EndDate = availabilityData.EndDate,
            Days = availabilityData.Days,
            StartTime = availabilityData.StartTime,
            EndTime = availabilityData.EndTime,
            UserID = availabilityData.UserID
        };

        db.PriestAvailabilities.Add(newAvailability);
    }

    await db.SaveChangesAsync();
    return Results.Created($"/priestavailabilities/{availabilityData.ID}", availabilityData);
});

app.MapGet("/", () => "Hello World!");

app.MapPost("/appointments", async (HttpContext context, AppDbContext db) =>
{
    var emailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    if (string.IsNullOrEmpty(emailClaim)) return Results.Unauthorized();

    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == emailClaim);
    if (user == null) return Results.NotFound("User not found.");

    var body = await context.Request.ReadFromJsonAsync<AppointmentRequest>();
    if (body == null) return Results.BadRequest("Invalid appointment data.");

    var appt = new Appointment
    {
        UserID = user.ID,
        Name = body.Name ?? string.Empty,
        Location = body.Location ?? string.Empty,
        Date = body.Date ?? string.Empty,
        Time = body.Time ?? string.Empty,
    };
    db.Appointments.Add(appt);
    await db.SaveChangesAsync();
    return Results.Created($"/appointments/{appt.ID}", appt);
});

app.MapGet("/appointments", async (HttpContext context, AppDbContext db) =>
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

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

// Remaining endpoints (user authentication, profiles, meetings, etc.)
app.MapGet("/login", async (HttpContext context) =>
{
    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/post-login"
    });
});

app.MapGet("/post-login", async (HttpContext context, AppDbContext db) =>
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


app.MapGet("/profile", async (HttpContext context, AppDbContext db) =>
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
        IsAdmin = IsAdmin(context)
    };

    return Results.Ok(result);
});

app.MapPost("/profile", async (HttpContext context, AppDbContext db) =>
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
        IsAdmin = IsAdmin(context)
    };

    return Results.Ok(result);
});




app.MapGet("/users", async (HttpContext context, AppDbContext db) =>
{
    var emailClaim = context.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
    if (emailClaim != "heneinfilobatire@gmail.com")
    {
        return Results.Unauthorized();
    }

    var users = await db.Users.ToListAsync();
    return Results.Ok(users);
});

app.MapPut("/users/{id}", async (int id, HttpContext context, User updatedUser, AppDbContext db) =>
{
    var emailClaim = context.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
    if (emailClaim != "heneinfilobatire@gmail.com")
    {
        return Results.Unauthorized();
    }

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

app.MapPost("/users/cancel", (HttpContext context) =>
{
    var emailClaim = context.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
    if (emailClaim != "heneinfilobatire@gmail.com")
    {
        return Results.Unauthorized();
    }

    // Logic to handle cancel operation
    return Results.Ok("Operation canceled.");
});

app.Run();

record AppointmentRequest(string? Name, string? Location, string? Date, string? Time);
record DemoLoginRequest(string? Role);
