namespace Warehouse.Application.Tests.Mapping;

public sealed class ApplicationMappingTests
{
    [Fact]
    public async Task AllApplicationMappings_AreValid()
    {
        await using var context = new ApplicationTestContext();

        context.Mapper.ConfigurationProvider.AssertConfigurationIsValid();
    }
}
