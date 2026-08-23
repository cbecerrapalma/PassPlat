using CBP.Data.Abstractions;
using CBP.Results;

namespace PassPlat.Datos;

internal static class SpHelper
{
    public static async Task<Result<T>> ExecuteSPAsync<T>(IRawQueryRepositoryAsync rawQuery, string spName, RawParameter[] parameters, string errorMsg, CancellationToken ct) where T : class, new()
    {
        var result = await rawQuery.QuerySPAsync<T>(spName, parameters, ct);
        if (!result.IsSuccess) return Result<T>.Failure(result.Error!);
        var item = result.Value.FirstOrDefault();
        return item != null ? Result<T>.Success(item) : Result<T>.Failure("SP_NO_RESULT", errorMsg);
    }
}