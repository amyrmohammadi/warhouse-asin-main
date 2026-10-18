using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application.Common.Interfaces.Repositories;

namespace Warehouse.Application.Tests.TestSupport;

internal sealed class ApplicationTestContext : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;

    public ApplicationTestContext()
    {
        IdentityService = Substitute.For<IIdentityService>();
        AuditTrailService = Substitute.For<IAuditTrailService>();
        UnitOfWork = Substitute.For<IUnitOfWork<Guid>>();
        UserAccessService = Substitute.For<IUserAccessService>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(IdentityService);
        services.AddSingleton(AuditTrailService);
        services.AddSingleton(UnitOfWork);
        services.AddSingleton(UserAccessService);

        _serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        _scope = _serviceProvider.CreateAsyncScope();
        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        Mapper = _scope.ServiceProvider.GetRequiredService<IMapper>();
    }

    public IIdentityService IdentityService { get; }

    public IAuditTrailService AuditTrailService { get; }

    public IUnitOfWork<Guid> UnitOfWork { get; }

    public IUserAccessService UserAccessService { get; }

    public ISender Sender { get; }

    public IMapper Mapper { get; }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }
}
