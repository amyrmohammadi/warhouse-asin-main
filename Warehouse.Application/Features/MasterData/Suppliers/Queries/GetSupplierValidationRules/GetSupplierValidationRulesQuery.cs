using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Validation;

namespace Warehouse.Application.Features.MasterData.Suppliers.Queries.GetSupplierValidationRules;

public sealed record GetSupplierValidationRulesQuery
    : IRequest<Result<IReadOnlyDictionary<string, FieldValidationRules>>>;
