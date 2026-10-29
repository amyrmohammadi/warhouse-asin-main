namespace Warehouse.Application.Common.Models;

public sealed class ExcelToPdfConversionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
