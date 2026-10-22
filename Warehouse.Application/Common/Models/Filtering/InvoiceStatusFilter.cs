using System.Text.Json.Serialization;

namespace Warehouse.Application.Common.Models.Filtering;

public enum InvoiceStatusFilter
{
    [JsonStringEnumMemberName("NotEntered")]
    NotEntered,

    [JsonStringEnumMemberName("DoesNotHave")]
    DoesNotHave,

    [JsonStringEnumMemberName("HasInvoice")]
    HasInvoice
}
