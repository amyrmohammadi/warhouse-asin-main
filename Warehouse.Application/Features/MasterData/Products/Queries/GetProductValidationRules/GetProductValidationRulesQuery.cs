using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Validation;

namespace Warehouse.Application.Features.MasterData.Products.Queries.GetProductValidationRules;

public sealed record GetProductValidationRulesQuery
    : IRequest<Result<IReadOnlyDictionary<string, FieldValidationRules>>>;
