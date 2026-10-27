using Warehouse.Application.Common.Authorization;

namespace Warehouse.Application.Tests.Common.Authorization;

public sealed class PermissionPolicyNamesTests
{
    [Fact]
    public void Groups_ContainEveryPermissionExactlyOnce()
    {
        var groupedPolicies = PermissionPolicyNames.Groups
            .SelectMany(group => group.Policies)
            .Select(policy => policy.Name)
            .ToArray();

        Assert.Equal(Enum.GetNames<AppPermission>().Order(), groupedPolicies.Order());
        Assert.Equal(groupedPolicies.Length, groupedPolicies.Distinct().Count());
        Assert.All(
            PermissionPolicyNames.Groups,
            group =>
            {
                Assert.False(string.IsNullOrWhiteSpace(group.Key));
                Assert.False(string.IsNullOrWhiteSpace(group.DisplayName));
                Assert.NotEmpty(group.Policies);
            });
    }

    [Fact]
    public void Groups_SeparateGoodsReceiptsFromGoodsIssues()
    {
        var receiptGroup = PermissionPolicyNames.Groups.Single(group => group.Key == "goods-receipts");
        var issueGroup = PermissionPolicyNames.Groups.Single(group => group.Key == "goods-issues");

        Assert.Equal("ورود کالا", receiptGroup.DisplayName);
        Assert.All(receiptGroup.Policies, policy => Assert.StartsWith("GoodsReceipt", policy.Name));
        Assert.Equal("خروج کالا", issueGroup.DisplayName);
        Assert.All(issueGroup.Policies, policy => Assert.StartsWith("GoodsIssue", policy.Name));
        Assert.DoesNotContain(PermissionPolicyNames.Groups, group => group.Key == "inventory");
    }

    [Fact]
    public void Groups_ExposeInvoicesAsAnIndependentPermissionGroup()
    {
        var invoiceGroup = PermissionPolicyNames.Groups.Single(group => group.Key == "invoices");

        var policies = invoiceGroup.Policies.Select(policy => policy.Name).ToArray();
        Assert.Equal("فاکتورها", invoiceGroup.DisplayName);
        Assert.Equal(
            [PermissionPolicyNames.Inventory.InvoicesRead,
                PermissionPolicyNames.Inventory.InvoicesMissingReceiptCountRead],
            policies);
    }
}
