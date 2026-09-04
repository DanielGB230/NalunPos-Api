using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Pos.Api.BackgroundServices;
using Pos.Api.Middleware;
using Pos.Application;
using Pos.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Inyección de dependencias de capas Clean Architecture
builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);

// Registro de Background Worker para el patrón Transactional Outbox
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();

string jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!";
string jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "NalunPosApi";
string jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "NalunPosClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options =>
    {
        options.WithTitle("Nalun POS API Documentation")
               .WithTheme(ScalarTheme.Purple);
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Hacer la clase Program accesible para WebApplicationFactory en tests de integración futuros
public partial class Program;
