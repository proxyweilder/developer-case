SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'Case2Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 50001,'Schema lock unavailable',1;
-- Existing desktop tables are never altered or dropped.
IF OBJECT_ID('dbo.Cariler') IS NULL
CREATE TABLE dbo.Cariler(
 CariKod NVARCHAR(20) PRIMARY KEY,
 Grup NVARCHAR(10) NOT NULL,
 KrediLimiti DECIMAL(18,2) NOT NULL,
 Bakiye DECIMAL(18,2) NOT NULL
);
IF OBJECT_ID('dbo.Teklifler') IS NULL
CREATE TABLE dbo.Teklifler(
 Id INT IDENTITY PRIMARY KEY,
 CariKod NVARCHAR(20) NOT NULL,
 Tutar DECIMAL(18,2) NOT NULL,
 KdvliTutar DECIMAL(18,2) NOT NULL,
 Tarih DATETIME NOT NULL
);
COMMIT;
