namespace MarketingApp.API.Helpers;

/// <summary>
/// Defensive clamps for pagination query parameters so malformed clients (or attackers
/// probing endpoints) can never trigger an underlying Skip(-N) / Take(huge) exception.
/// Without this guard, BUG-003 surfaced: `?pageNumber=-1` returned HTTP 500.
///
/// Call this at the top of every list/paged controller action right after the
/// parameters are read. The values are clamped IN PLACE via ref so the caller
/// can use them unchanged afterwards.
/// </summary>
public static class PagingHelper
{
    /// <summary>Reasonable max page size — large enough for CSV-bulk views, small enough
    /// to prevent a single request from pulling tens of thousands of rows.</summary>
    public const int MaxPageSize = 200;

    /// <summary>Clamp pageNumber to >= 1 and pageSize to [1, MaxPageSize].</summary>
    public static void Clamp(ref int pageNumber, ref int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 1;
        else if (pageSize > MaxPageSize) pageSize = MaxPageSize;
    }
}
