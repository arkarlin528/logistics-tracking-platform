namespace Logistics.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Mapped to HTTP 404. Also used when a customer asks for another customer's data, so we don't reveal that it exists.</summary>
public sealed class NotFoundException(string message) : Exception(message);
