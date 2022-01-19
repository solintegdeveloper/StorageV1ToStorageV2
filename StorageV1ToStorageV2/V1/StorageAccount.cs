namespace StorageV1ToStorageV2.V1;
public class StorageAccount
{
    public Guid IdCuenta { get; set; }
    public string? Nombre { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }
}
