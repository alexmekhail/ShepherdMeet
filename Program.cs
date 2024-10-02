using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using MinimalApiProject.Models;
using System.Text.Json;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000") // Replace with your frontend URL
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax; 
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.CallbackPath = "/signin-google"; 
    options.Scope.Add("email");
    options.Scope.Add("profile");
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors("AllowSpecificOrigin");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/priestavailabilities", async (AppDbContext db) => {
    var availabilityList = await db.PriestAvailabilities.ToListAsync();

    // Format the response to include formatted times
    var formattedAvailabilityList = availabilityList.Select(availability => new
    {
        availability.UserID,
        startDate = availability.StartDate.ToString("yyyy-MM-dd"),
        endDate = availability.EndDate.ToString("yyyy-MM-dd"),
        Days = availability.Days,
        startTime = availability.StartDate.ToString("hh:mm tt"),
        endTime = availability.EndDate.ToString("hh:mm tt"),
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

app.MapDelete("/priestavailabilities", async (AppDbContext db) => {
    db.PriestAvailabilities.RemoveRange(db.PriestAvailabilities);
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
                // User exists, redirect to profile
                return Results.Redirect("http://localhost:3000/");
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

                var htmlContent = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Account Created</title>
                </head>
                <body>
                    <h1>Account Created</h1>
                    <p>An account for {nameClaim} has been created.</p>
                    <script>
                        setTimeout(function() {{
                            window.location.href = '/profile';
                        }}, 3000);
                    </script>
                </body>
                </html>";

                return Results.Content(htmlContent, "text/html");
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
        LastName = user.LastName
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
        LastName = user.LastName
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

//remove later
//work on backend for meeting
app.MapGet("/delete-all-users", async (AppDbContext db) =>
{
    async Task DeleteAllUsers()
    {
        var users = await db.Users.ToListAsync();
        db.Users.RemoveRange(users);
        await db.SaveChangesAsync();
    }

    await DeleteAllUsers();
    return Results.Ok("All users have been deleted.");
});


app.Run();