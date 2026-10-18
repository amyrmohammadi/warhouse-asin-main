namespace Warehouse.Application.Common.Models.Validation;

public sealed record FieldValidationRules(
    bool Required,
    int? MaxLength = null);
