using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.People.Commands.UpdatePerson;

internal sealed class UpdatePersonCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdatePersonCommand, Result<PersonSummary>>
{
    public async Task<Result<PersonSummary>> Handle(
        UpdatePersonCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Person>();
        var person = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (person is null)
        {
            return Result<PersonSummary>.Error("The requested person was not found.");
        }

        if (!person.TryAdvanceConcurrencyStamp(request.ConcurrencyStamp))
        {
            return Result<PersonSummary>.Error(ConcurrencyMessages.Conflict);
        }

        var duplicateMessage = await PersonDataHelper.FindDuplicateMessageAsync(
            unitOfWork,
            request.FullName,
            request.Id,
            cancellationToken);
        if (duplicateMessage is not null)
        {
            return Result<PersonSummary>.Error(duplicateMessage);
        }

        person.Update(request.FullName, request.PhoneNumber);
        await repository.UpdateAsync(person, cancellationToken);
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
