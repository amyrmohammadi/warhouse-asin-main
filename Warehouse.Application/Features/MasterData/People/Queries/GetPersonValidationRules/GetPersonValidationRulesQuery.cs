using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Validation;

namespace Warehouse.Application.Features.MasterData.People.Queries.GetPersonValidationRules;

public sealed record GetPersonValidationRulesQuery
    : IRequest<Result<IReadOnlyDictionary<string, FieldValidationRules>>>;
