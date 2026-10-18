using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Features.Identity.Queries.GetRoles;

namespace Warehouse.Application.Tests.Features.Identity.Queries.GetRoles;

public sealed class GetRolesValidationTests
{
    [Fact]
    public async Task Send_WithInvalidBetweenRange_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var query = new GetRolesQuery
        {
            UserCount = new NumericFilter<int>
            {
                Operator = NumericOperator.Between,
                Value = 10,
                ValueTo = 2
            }
        };

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(query));
    }

    [Fact]
    public async Task Send_WithNegativeCount_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var query = new GetRolesQuery
        {
            PermissionCount = new NumericFilter<int>
            {
                Operator = NumericOperator.Gte,
                Value = -1
            }
        };

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(query));
    }

    [Fact]
    public async Task Send_WithMissingBetweenEnd_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var query = new GetRolesQuery
        {
            UserCount = new NumericFilter<int>
            {
                Operator = NumericOperator.Between,
                Value = 0
            }
        };

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(query));
    }
}
