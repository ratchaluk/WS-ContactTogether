using System.Text;
using ContactTogetherApi.Auth;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Helper;
using ContactTogetherApi.Reports.Export;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication ------------------------------------------------------------

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

if (jwtSettings.Key.Length < 32)
{
    throw new InvalidOperationException(
        $"{JwtSettings.SectionName}:Key must be at least 32 characters. Set it in user secrets: " +
        $"dotnet user-secrets set \"{JwtSettings.SectionName}:Key\" \"<random value>\"");
}

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenRevocationStore, MemoryTokenRevocationStore>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordVerifier, PasswordVerifier>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep "sub", "jti" and friends as-is instead of rewriting them to WS-Federation claim URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = AuthClaimTypes.Role,
        };

        // Logout revokes a token by its jti; reject it here for the rest of its lifetime.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var tokenId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                var store = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationStore>();

                if (string.IsNullOrEmpty(tokenId) || store.IsRevoked(tokenId))
                {
                    context.Fail("This token has been revoked.");
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

// Report export (ExportReport01-07) ----------------------------------------

QuestPDF.Settings.License = LicenseType.Community;
// A character the font cannot draw becomes a placeholder instead of failing the whole export.
QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
ReportFonts.Register();

var exportSection = builder.Configuration.GetSection(ReportExportOptions.SectionName);
builder.Services.Configure<ReportExportOptions>(exportSection);
var exportOptions = exportSection.Get<ReportExportOptions>() ?? new ReportExportOptions();

builder.Services.AddRateLimiter(options =>
{
    // Exports are heavy (whole date range, file generation), so only a few may run at once.
    options.AddConcurrencyLimiter(ReportExportOptions.RateLimitPolicy, limiter =>
    {
        limiter.PermitLimit = Math.Max(1, exportOptions.MaxConcurrent);
        limiter.QueueLimit = 0;
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new MessageResponse(StatusCodes.Status429TooManyRequests, "มีการ Export อื่นกำลังทำงาน กรุณาลองใหม่อีกครั้ง"),
            cancellationToken);
    };
});

// ---------------------------------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by /Auth/login (without the \"Bearer \" prefix).",
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = [],
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.OpenSwaggerOnStart();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
