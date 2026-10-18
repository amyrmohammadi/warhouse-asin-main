using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.DeleteSupplier;

public sealed record DeleteSupplierCommand(Guid Id) : IRequest<Result>;
