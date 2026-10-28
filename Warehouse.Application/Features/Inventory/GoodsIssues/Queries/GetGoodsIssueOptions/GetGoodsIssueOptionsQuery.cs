using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueOptions;

public sealed record GetGoodsIssueOptionsQuery : IRequest<Result<GoodsIssueOptions>>;

internal sealed class GetGoodsIssueOptionsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsIssueOptionsQuery, Result<GoodsIssueOptions>>
{
    public async Task<Result<GoodsIssueOptions>> Handle(
        GetGoodsIssueOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var peopleRepository = unitOfWork.Repository<Person>();
        var unitRepository = unitOfWork.Repository<UnitOfMeasure>();
        var people = await peopleRepository.ToListAsync(
            peopleRepository.Entities.OrderBy(person => person.FullName).Select(person => new PersonSummary
            {
                Id = person.Id,
                FullName = person.FullName,
                PhoneNumber = person.PhoneNumber
            }), cancellationToken);
        var units = await unitRepository.ToListAsync(
            unitRepository.Entities
                .OrderBy(unit => unit.Title)
                .Select(unit => new TitledMasterDataSummary
                {
                    Id = unit.Id,
                    ConcurrencyStamp = unit.ConcurrencyStamp,
                    Title = unit.Title,
                    Description = unit.Description
                }), cancellationToken);
        return Result<GoodsIssueOptions>.Success(new GoodsIssueOptions
        {
            People = people,
            UnitsOfMeasure = units
        });
    }
}
