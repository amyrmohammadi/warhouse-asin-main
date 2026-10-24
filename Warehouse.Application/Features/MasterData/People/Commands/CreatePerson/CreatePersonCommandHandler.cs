using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.People.Commands.CreatePerson;

internal sealed class CreatePersonCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<CreatePersonCommand, Result<PersonSummary>>
{
    public async Task<Result<PersonSummary>> Handle(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        var duplicateMessage = await PersonDataHelper.FindDuplicateMessageAsync(
            unitOfWork,
            request.FullName,
            null,
            cancellationToken);
        if (duplicateMessage is not null)
        {
            return Result<PersonSummary>.Error(duplicateMessage);
        }

        var person = new Person(request.FullName, request.PhoneNumber);
        await unitOfWork.Repository<Person>().AddAsync(person, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);

        return Result<PersonSummary>.Success(new PersonSummary
        {
            Id = person.Id,
            ConcurrencyStamp = person.ConcurrencyStamp,
            FullName = person.FullName,
            PhoneNumber = person.PhoneNumber
        });
    }
}
