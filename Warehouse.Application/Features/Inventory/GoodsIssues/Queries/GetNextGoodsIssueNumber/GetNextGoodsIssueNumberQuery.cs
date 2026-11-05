using System.Globalization;
using System.Numerics;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetNextGoodsIssueNumber;

public sealed record GetNextGoodsIssueNumberQuery : IRequest<Result<string>>;

internal sealed class GetNextGoodsIssueNumberQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetNextGoodsIssueNumberQuery, Result<string>>
{
    public async Task<Result<string>> Handle(GetNextGoodsIssueNumberQuery request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssue>();
        var numbers = await repository.ToListAsync(repository.Entities
            .Where(issue => issue.IssueNumber.StartsWith("99/") || issue.IssueNumber.StartsWith("۹۹/") || issue.IssueNumber.StartsWith("٩٩/"))
            .Select(issue => issue.IssueNumber), cancellationToken);
        var maximum = BigInteger.Zero;
        foreach (var number in numbers)
        {
            var suffix = string.Concat(number[3..].Trim().Select(character => character switch
            {
                >= '۰' and <= '۹' => (char)('0' + character - '۰'),
                >= '٠' and <= '٩' => (char)('0' + character - '٠'),
                _ => character
            }));
            if (!BigInteger.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) continue;
            if (value < maximum) continue;
            maximum = value;
        }
        var next = "99/" + (maximum + 1).ToString(CultureInfo.InvariantCulture).PadLeft(5, '0');
        return next.Length <= 100 ? Result<string>.Success(next)
            : Result<string>.Error("شماره بعدی خروج از طول مجاز بیشتر است؛ شماره را دستی وارد کنید.");
    }
}
