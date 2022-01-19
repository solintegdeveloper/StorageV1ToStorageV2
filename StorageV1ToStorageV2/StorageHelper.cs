using Dapper;

namespace StorageV1ToStorageV2;

public static class StorageHelper
{
    public static async Task<List<V1.StorageAccount>> GetV1StorageAccounts(this string server)
    {
        await using var db = await DatabaseHelper.GetConnection(server, "AutomatizerDocumentDB");
        var accounts = await db.QueryAsync<V1.StorageAccount>("SELECT * FROM StorageAcounts");
        return accounts.ToList();
    }
}
