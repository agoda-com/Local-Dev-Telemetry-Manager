namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record FilterParams(
    string? Environment,
    string? Platform,
    string? Project,
    string? Repository,
    string? Branch,
    string? BuildCategory,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 50);
