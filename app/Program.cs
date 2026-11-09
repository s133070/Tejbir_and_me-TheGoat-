using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Unbreakable.Data;
using Unbreakable.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<UnbreakableContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Unbreakable")));

var app = builder.Build();

// Databank aanmaken. Geen voorbeeldberichten meer: de databank bevat enkel ciphertext.
// De databank kan iets later opstarten dan de app, dus we proberen een paar keer.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<UnbreakableContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            context.Database.EnsureCreated();
            break;
        }
        catch (Exception ex) when (attempt < 30)
        {
            logger.LogWarning("Databank nog niet bereikbaar ({Error}), poging {Attempt}", ex.Message, attempt);
            Thread.Sleep(2000);
        }
    }
}

// Stack traces enkel tonen tijdens ontwikkeling, niet in productie.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// De SPA: index.html, JavaScript en CSS uit wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => "ok");

// De API. Wie je bent, staat voorlopig in de header X-User.
var api = app.MapGroup("/api");

api.MapGet("/users", async (UnbreakableContext db) =>
{
    var senders = await db.Messages.Select(m => m.Sender).Distinct().ToListAsync();
    var recipients = await db.Messages.Select(m => m.Recipient).Distinct().ToListAsync();
    return Results.Ok(senders.Union(recipients).OrderBy(name => name).ToList());
});

api.MapGet("/messages", async (HttpRequest request, UnbreakableContext db) =>
{
    var user = CurrentUser(request);
    if (string.IsNullOrEmpty(user))
    {
        return Results.Unauthorized();
    }

    var inbox = db.Messages.Where(m => m.Recipient == user);

    var from = request.Query["from"].ToString();
    if (!string.IsNullOrEmpty(from))
    {
        inbox = inbox.Where(m => m.Sender == from);
    }

    return Results.Ok(await inbox.OrderByDescending(m => m.SentAt).ToListAsync());
});

api.MapGet("/messages/search", async (HttpRequest request, UnbreakableContext db) =>
{
    var user = CurrentUser(request);
    if (string.IsNullOrEmpty(user))
    {
        return Results.Unauthorized();
    }

    // Zoeken in ciphertext heeft geen zin; enkel op afzender, met een geparametriseerde query (geen SQL-injectie).
    var q = request.Query["q"].ToString();
    var messages = await db.Messages
        .Where(m => m.Recipient == user && EF.Functions.ILike(m.Sender, "%" + q + "%"))
        .OrderByDescending(m => m.SentAt)
        .ToListAsync();
    return Results.Ok(messages);
});

api.MapGet("/messages/export", async (HttpRequest request, UnbreakableContext db) =>
{
    var user = CurrentUser(request);
    if (string.IsNullOrEmpty(user))
    {
        return Results.Unauthorized();
    }

    var messages = await db.Messages
        .Where(m => m.Recipient == user)
        .OrderBy(m => m.SentAt)
        .ToListAsync();

    return Results.Text(JsonConvert.SerializeObject(messages, Formatting.Indented), "application/json");
});

api.MapGet("/messages/{id:int}", async (int id, HttpRequest request, UnbreakableContext db) =>
{
    var user = CurrentUser(request);
    if (string.IsNullOrEmpty(user))
    {
        return Results.Unauthorized();
    }

    // Enkel afzender en ontvanger mogen een bericht opvragen.
    var message = await db.Messages.FindAsync(id);
    return message == null || (message.Recipient != user && message.Sender != user)
        ? Results.NotFound()
        : Results.Ok(message);
});

api.MapPost("/messages", async (NewMessage input, HttpRequest request, UnbreakableContext db) =>
{
    var user = CurrentUser(request);
    if (string.IsNullOrEmpty(user))
    {
        return Results.Unauthorized();
    }

    // Alles valideren: enkel geldige ciphertext, lijnnummer en MAC worden opgeslagen.
    var errors = new List<ValidationResult>();
    if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, true))
    {
        return Results.ValidationProblem(errors
            .GroupBy(e => e.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }

    // Onderwerp en bericht delen één codeboeklijn van 40 tekens.
    if (input.Subject.Length + input.Body.Length > 40)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Body"] = ["Onderwerp en bericht samen mogen maximaal 40 tekens zijn."]
        });
    }

    // Afzender komt van wie je bent, niet uit het verzoek (geen spoofing via de body).
    var message = new Message
    {
        Sender = user,
        Recipient = input.Recipient,
        Subject = input.Subject,
        Body = input.Body,
        KeyLine = input.KeyLine,
        Mac = input.Mac,
        SentAt = DateTime.UtcNow
    };

    // Lijn al gebruikt tussen deze twee? Weigeren (hergebruik van de sleutel of replay).
    if (await db.Messages.AnyAsync(m => m.Sender == user && m.Recipient == message.Recipient && m.KeyLine == message.KeyLine))
    {
        return Results.Conflict("Deze codeboeklijn is al gebruikt.");
    }

    db.Messages.Add(message);
    await db.SaveChangesAsync();

    return Results.Created($"/api/messages/{message.Id}", message);
});

api.MapDelete("/messages/{id:int}", async (int id, HttpRequest request, UnbreakableContext db) =>
{
    var user = CurrentUser(request);
    if (string.IsNullOrEmpty(user))
    {
        return Results.Unauthorized();
    }

    // Enkel de ontvanger mag een bericht verwijderen.
    var message = await db.Messages.FindAsync(id);
    if (message == null || message.Recipient != user)
    {
        return Results.NotFound();
    }

    db.Messages.Remove(message);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

static string CurrentUser(HttpRequest request)
{
    return request.Headers["X-User"].ToString();
}
