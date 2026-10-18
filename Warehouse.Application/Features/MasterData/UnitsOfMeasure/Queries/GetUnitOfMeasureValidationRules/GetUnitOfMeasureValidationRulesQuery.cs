using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Validation;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitOfMeasureValidationRules;

public sealed record GetUnitOfMeasureValidationRulesQuery
    : IRequest<Result<IReadOnlyDictionary<string, FieldValidationRules>>>;
