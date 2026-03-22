namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record PaginatedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
