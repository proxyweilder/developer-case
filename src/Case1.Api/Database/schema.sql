SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'Case1Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 50001,'Schema lock unavailable',1;
IF OBJECT_ID('dbo.Products') IS NULL
CREATE TABLE dbo.Products(
 Code nvarchar(40) NOT NULL PRIMARY KEY,
 Physical int NOT NULL,
 Reserved int NOT NULL DEFAULT 0,
 CONSTRAINT CK_Products_Stock CHECK(Physical>=0 AND Reserved>=0 AND Reserved<=Physical)
);
IF OBJECT_ID('dbo.Orders') IS NULL
CREATE TABLE dbo.Orders(
 Id uniqueidentifier NOT NULL PRIMARY KEY,
 RequestKey varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL UNIQUE,
 RequestHash char(64) NOT NULL,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
IF OBJECT_ID('dbo.OrderLines') IS NULL
CREATE TABLE dbo.OrderLines(
 OrderId uniqueidentifier NOT NULL REFERENCES dbo.Orders(Id),
 ProductCode nvarchar(40) NOT NULL REFERENCES dbo.Products(Code),
 Requested int NOT NULL,
 Reserved int NOT NULL,
 Cancelled int NOT NULL DEFAULT 0,
 PRIMARY KEY(OrderId,ProductCode),
 CONSTRAINT CK_OrderLines_Quantity CHECK(Requested>0 AND Reserved>=0 AND Reserved<=Requested AND Cancelled>=0 AND Cancelled<=Reserved)
);
-- Restarting must never reset live stock.
IF NOT EXISTS(SELECT 1 FROM dbo.Products WHERE Code=N'URN-001') INSERT dbo.Products(Code,Physical) VALUES(N'URN-001',10);
IF NOT EXISTS(SELECT 1 FROM dbo.Products WHERE Code=N'URN-002') INSERT dbo.Products(Code,Physical) VALUES(N'URN-002',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Products WHERE Code=N'URN-003') INSERT dbo.Products(Code,Physical) VALUES(N'URN-003',0);
COMMIT;
