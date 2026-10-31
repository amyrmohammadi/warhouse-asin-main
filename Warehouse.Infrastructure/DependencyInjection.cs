using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Infrastructure.Documents;
using Warehouse.Infrastructure.Identity;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Persistence.Auditing;
using Warehouse.Infrastructure.Persistence.Repositories;

namespace Warehouse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is not configured.");

            options.UseSqlServer(connectionString);
        });

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services
            .AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName);

        services
            .AddOptions<IdentitySeedOptions>()
            .BindConfiguration(IdentitySeedOptions.SectionName);

        services
            .AddAuthentication(authenticationOptions =>
            {
                authenticationOptions.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;
                authenticationOptions.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                var isConfigured = JwtOptionsValidator.IsConfigured(jwtOptions);

                bearerOptions.MapInboundClaims = false;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = isConfigured ? jwtOptions.Issuer : "__jwt_not_configured__",
                    ValidateAudience = true,
                    ValidAudience = isConfigured ? jwtOptions.Audience : "__jwt_not_configured__",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtSecurityKeyFactory.CreateForValidation(
                        jwtOptions.SecretKey),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IIdentitySeeder, IdentitySeeder>();

        services.AddOptions<LibreOfficeOptions>()
            .BindConfiguration(LibreOfficeOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(options => Path.IsPathFullyQualified(options.ExecutablePath) &&
                                 Path.IsPathFullyQualified(options.TempRootPath),
                "LibreOffice executable and temporary root must be absolute paths.")
            .ValidateOnStart();
        services.AddSingleton<LibreOfficeExcelToPdfService>();
        services.AddSingleton<IExcelToPdfService>(provider => provider.GetRequiredService<LibreOfficeExcelToPdfService>());
        services.AddSingleton<IGoodsIssuePdfService, GoodsIssuePdfService>();
        services.AddSingleton<IInvoicePdfService, InvoicePdfService>();
        services.AddHostedService<ExcelToPdfTempCleanupService>();

        return services;
    }
}
