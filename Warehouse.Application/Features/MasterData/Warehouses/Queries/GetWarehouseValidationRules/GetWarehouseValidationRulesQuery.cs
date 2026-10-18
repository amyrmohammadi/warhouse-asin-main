using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Validation;

namespace Warehouse.Application.Features.MasterData.Warehouses.Queries.GetWarehouseValidationRules;

public sealed record GetWarehouseValidationRulesQuery
    : IRequest<Result<IReadOnlyDictionary<string, FieldValidationRules>>>;
