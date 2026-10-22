using Warehouse.Application.Features.Auditing.Queries.GetGoodsReceiptItemHistory;
using Warehouse.Application.Common.Models.Auditing;

namespace Warehouse.Application.Tests.Features.Auditing.Queries.GetGoodsReceiptItemHistory;

public sealed class GetGoodsReceiptItemHistoryTests
{
    [Fact]
    public async Task Send_WhenItemExists_ReturnsReadableHistory()
    {
        await using var context = new ApplicationTestContext();
        var itemId = Guid.NewGuid();
        var expected = new GoodsReceiptItemHistoryResult(
            itemId,
            "ورق فولادی (P-1)",
            [new EntityHistoryEntry(
                1,
                "Update",
                "ویرایش",
                "اطلاعات کالا",
                "کاربر آزمون",
                DateTimeOffset.UtcNow,
                [new EntityHistoryChange("Quantity", "تعداد", "1", "2")])]);
        context.AuditTrailService
            .GetGoodsReceiptItemHistoryAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await context.Sender.Send(new GetGoodsReceiptItemHistoryQuery(itemId));

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Data);
    }

    [Fact]
    public async Task Send_WithEmptyItemId_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(new GetGoodsReceiptItemHistoryQuery(Guid.Empty)));
    }
}
