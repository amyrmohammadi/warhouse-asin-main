namespace Warehouse.Application.Common.Interfaces;

public interface IExcelToPdfService
{
    Task<byte[]> ConvertAsync(byte[] excelBytes, CancellationToken cancellationToken = default);
}
