using IdentityService.Api.Controllers;
using IdentityService.Application;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Features.Identity.Services;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database.Context;
using IdentityService.Infrastructure.Database.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace IdentityService.Api.Tests.Support;

public sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

public sealed class FakeOtp : IOtpClient
{
    public bool VerifyResult { get; set; } = true;

    public List<string> Sent { get; } = new();

    public Task SendCodeAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        Sent.Add(phoneNumber);
        return Task.CompletedTask;
    }

    public Task<bool> VerifyCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
        => Task.FromResult(VerifyResult);
}

/// <summary>Real MediatR pipeline and real handlers over SQLite in-memory; only the OTP port is faked.</summary>
public sealed class ApiFixture : IDisposable
{
    public const string Secret = "api-test-signing-key-which-is-at-least-32-bytes";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _provider;

    public ApiFixture(IdentityClientOptions? clients = null)
    {
        _connection.Open();
        Jwt = new JwtOptions { Issuer = "api-tests", Audience = "api-tests-clients", Secret = Secret, AccessTokenMinutes = 10 };
        Clients = clients ?? new IdentityClientOptions();
        Otp = new FakeOtp();

        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IOtpClient>(Otp);
        services.AddSingleton(Options.Create(Jwt));
        services.AddSingleton(Options.Create(Clients));
        services.AddSingleton(Options.Create(new RootAdminOptions()));
        services.AddScoped<IJwtService, JwtService>();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(AssemblyMarker).Assembly));
        _provider = services.BuildServiceProvider();

        Scope = _provider.CreateScope();
        Scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreated();
    }

    public JwtOptions Jwt { get; }

    public IdentityClientOptions Clients { get; }

    public FakeOtp Otp { get; }

    public IServiceScope Scope { get; }

    public IUnitOfWork Uow => Scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    public IdentityDbContext Db => Scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

    public IdentityController Identity()
        => new(Scope.ServiceProvider.GetRequiredService<IMediator>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    public OidcController Oidc(TokenValidationParameters? validation = null)
    {
        var bearer = new JwtBearerOptions
        {
            TokenValidationParameters = validation ?? new TokenValidationParameters
            {
                ValidIssuer = Jwt.Issuer,
                ValidAudience = Jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                ClockSkew = TimeSpan.Zero
            }
        };
        return new OidcController(
            Options.Create(Jwt),
            Options.Create(Clients),
            new StaticOptionsMonitor<JwtBearerOptions>(bearer),
            Scope.ServiceProvider.GetRequiredService<IMediator>(),
            Uow)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public void Dispose()
    {
        Scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }
}
