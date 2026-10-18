using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.People.Queries.GetPersonById;

internal sealed class GetPersonByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetPersonByIdQuery, Result<PersonSummary>>
{
    public async Task<Result<PersonSummary>> Handle(
        GetPersonByIdQuery request,
        CancellationToken cancellationToken)
    {
        var person = await unitOfWork
            .Repository<Person>()
            .GetByIdAsync(request.Id, cancellationToken);

        return person is null
            ? Result<PersonSummary>.Error("The requested person was not found.")
            : Result<PersonSummary>.Success(new PersonSummary
            {
                Id = person.Id,
                FullName = person.FullName,
                PhoneNumber = person.PhoneNumber
            });
    }
}
