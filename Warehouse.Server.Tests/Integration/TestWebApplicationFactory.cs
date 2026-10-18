using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Warehouse.Application.Common.Interfaces;

namespace Warehouse.Server.Tests.Integration;

internal sealed class TestWebApplicationFactory(string environment = "Testing")
    : WebApplicationFactory<Program>
{
    public ISender Sender { get; } = Substitute.For<ISender>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = string.Empty
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISender>();
            services.AddSingleton(Sender);
            services.RemoveAll<IUserAccessService>();
            services.AddScoped<IUserAccessService, TestUserAccessService>();
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationDefaults.Scheme;
                    options.DefaultChallengeScheme = TestAuthenticationDefaults.Scheme;
                    options.DefaultForbidScheme = TestAuthenticationDefaults.Scheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationDefaults.Scheme,
                    _ => { });
        });
    }
}
