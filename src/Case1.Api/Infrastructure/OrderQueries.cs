namespace Case1.Api.Infrastructure;

// SQL is kept here so the data flow can be reviewed independently from HTTP handling.
internal static class OrderQueries
{
    public const string AcquireRequestLock = """
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock
            @Resource = @resource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = 10000;
        SELECT @result;
        """;

    public const string PreviousOrder = """
        SELECT Id, RequestHash
        FROM dbo.Orders
        WHERE RequestKey = @requestKey;
        """;

    public const string StockForUpdate = """
        SELECT Code AS Kod, Physical AS Fiziksel, Reserved AS Rezerve
        FROM dbo.Products WITH (UPDLOCK, HOLDLOCK)
        WHERE Code = @code;
        """;

    public const string Stock = """
        SELECT Code AS Kod, Physical AS Fiziksel, Reserved AS Rezerve
        FROM dbo.Products
        WHERE Code = @code;
        """;

    public const string InsertOrder = """
        INSERT dbo.Orders (Id, RequestKey, RequestHash)
        VALUES (@id, @requestKey, @hash);
        """;

    // Products are already locked in sorted order before this batch is executed.
    // One command writes all lines instead of a separate network call per line.
    public const string ReserveAndInsertLines = """
        DECLARE @lines TABLE (
            Code nvarchar(40) PRIMARY KEY,
            Requested int,
            Quantity int
        );

        INSERT @lines (Code, Requested, Quantity)
        SELECT UrunKod, Istenen, Rezerve
        FROM OPENJSON(@linesJson)
        WITH (UrunKod nvarchar(40), Istenen int, Rezerve int);

        UPDATE product
        SET Reserved = product.Reserved + line.Quantity
        FROM dbo.Products AS product
        INNER JOIN @lines AS line ON line.Code = product.Code
        WHERE line.Quantity > 0;

        INSERT dbo.OrderLines (OrderId, ProductCode, Requested, Reserved)
        SELECT @id, Code, Requested, Quantity
        FROM @lines;
        """;

    public const string LockOrderForCancellation = """
        SELECT COUNT(*)
        FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @id;
        """;

    public const string CancelLine = """
        UPDATE dbo.Products
        SET Reserved = Reserved - @quantity
        WHERE Code = @code;

        UPDATE dbo.OrderLines
        SET Cancelled = Cancelled + @quantity
        WHERE OrderId = @id AND ProductCode = @code;
        """;

    public const string ReadOrder = """
        SELECT CreatedAt
        FROM dbo.Orders
        WHERE Id = @id;

        SELECT ProductCode AS UrunKod,
               Requested AS Istenen,
               Reserved AS Rezerve,
               Cancelled AS Iptal
        FROM dbo.OrderLines
        WHERE OrderId = @id
        ORDER BY ProductCode;
        """;
}
