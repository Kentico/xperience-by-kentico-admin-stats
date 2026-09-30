-- Seeds digital commerce orders for visual testing of the "Orders and revenue" report.
-- LOCAL DEV DB ONLY (DancingGoat example). Never run on a real project.
--
-- Creates ~25 customers (names and emails from contacts where available) and ~350 orders over the last 120 days:
-- weekday/weekend rhythm, a promo spike (days 40-45 back), a few days with no orders,
-- statuses spread (mostly Fulfilled / Payment received; recent orders more often Pending / Processing),
-- both payment and shipping methods (display names copied from the method tables),
-- 1-4 items per order from real DancingGoat products (SKU, name, price from content items).
-- Totals are consistent: item total = qty x unit price, item tax = item total x tax rate,
-- order total = sum of item totals, grand total = total + shipping + tax.
-- One billing address per order (addresses are not required by FKs; added so native order pages look complete).
-- Values are pseudo-random but deterministic (CHECKSUM of fixed strings), dates move with today.
--
-- Seeded orders have OrderNumber 'SEED-%'. Re-running deletes them (and their items, addresses, promotions and
-- customers without other orders) first.
--
-- Run (Git Bash):
--   MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' \
--     -d xperience-by-kentico-admin-stats -b < .agent-resources/seed-commerce-orders.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[Commerce_Order]') IS NULL
BEGIN
    PRINT 'Commerce tables not found; nothing to seed.';
    RETURN;
END;

DECLARE @Pattern nvarchar(20) = N'SEED-%';
DECLARE @TaxRate decimal(19, 4) = 21; -- percent, as DancingGoatTaxRateConstants.TAX_RATE
DECLARE @Today date = CAST(GETDATE() AS date);

BEGIN TRANSACTION;

-- 1. Remove previously seeded data.
DECLARE @OldCustomers TABLE ([CustomerID] int PRIMARY KEY);
INSERT INTO @OldCustomers
SELECT DISTINCT [OrderCustomerID] FROM [Commerce_Order] WHERE [OrderNumber] LIKE @Pattern;

DELETE P FROM [Commerce_OrderPromotion] P
    JOIN [Commerce_Order] O ON O.[OrderID] = P.[OrderPromotionOrderID]
    WHERE O.[OrderNumber] LIKE @Pattern;
DELETE A FROM [Commerce_OrderAddress] A
    JOIN [Commerce_Order] O ON O.[OrderID] = A.[OrderAddressOrderID]
    WHERE O.[OrderNumber] LIKE @Pattern;
DELETE I FROM [Commerce_OrderItem] I
    JOIN [Commerce_Order] O ON O.[OrderID] = I.[OrderItemOrderID]
    WHERE O.[OrderNumber] LIKE @Pattern;
DELETE FROM [Commerce_Order] WHERE [OrderNumber] LIKE @Pattern;
DELETE C FROM [Commerce_Customer] C
    JOIN @OldCustomers S ON S.[CustomerID] = C.[CustomerID]
    WHERE NOT EXISTS (SELECT 1 FROM [Commerce_Order] O WHERE O.[OrderCustomerID] = C.[CustomerID])
        AND NOT EXISTS (SELECT 1 FROM [Commerce_CustomerAddress] CA WHERE CA.[CustomerAddressCustomerID] = C.[CustomerID]);

-- 2. Lookups (by code name, so IDs are not assumed). Missing statuses fall back to the first status.
DECLARE @Fallback int = (SELECT TOP (1) [OrderStatusID] FROM [Commerce_OrderStatus] ORDER BY [OrderStatusOrder]);
IF @Fallback IS NULL
BEGIN
    PRINT 'No order statuses; nothing to seed.';
    ROLLBACK TRANSACTION;
    RETURN;
END;

DECLARE @Fulfilled int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'Fulfilled'), @Fallback);
DECLARE @Received int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'PaymentReceived'), @Fallback);
DECLARE @Processing int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'Processing'), @Fallback);
DECLARE @Pending int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'Pending'), @Fallback);
DECLARE @Failed int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'PaymentFailed'), @Fallback);

IF OBJECT_ID('tempdb..#Payment') IS NOT NULL DROP TABLE #Payment;
SELECT ROW_NUMBER() OVER (ORDER BY [PaymentMethodID]) - 1 AS [Idx], [PaymentMethodID], [PaymentMethodDisplayName]
INTO #Payment FROM [Commerce_PaymentMethod];

IF OBJECT_ID('tempdb..#Shipping') IS NOT NULL DROP TABLE #Shipping;
SELECT ROW_NUMBER() OVER (ORDER BY [ShippingMethodID]) - 1 AS [Idx], [ShippingMethodID], [ShippingMethodDisplayName], ISNULL([ShippingMethodPrice], 0) AS [Price]
INTO #Shipping FROM [Commerce_ShippingMethod];

DECLARE @PaymentCount int = (SELECT COUNT(*) FROM #Payment);
DECLARE @ShippingCount int = (SELECT COUNT(*) FROM #Shipping);

-- Products with a price (latest content item data), weighted so cheap items sell more often.
IF OBJECT_ID('tempdb..#Products') IS NOT NULL DROP TABLE #Products;
WITH [P] AS (
    SELECT [ProductSKUCode] AS [Sku], MAX([ProductFieldName]) AS [Name], MAX([ProductFieldPrice]) AS [Price]
    FROM [CMS_ContentItemCommonData]
    WHERE [ProductSKUCode] IS NOT NULL AND [ProductSKUCode] <> N'' AND [ProductFieldPrice] > 0 AND [ContentItemCommonDataIsLatest] = 1
    GROUP BY [ProductSKUCode]
), [W] AS (
    SELECT *, CASE WHEN [Price] >= 400 THEN 1 WHEN [Price] >= 50 THEN 3 WHEN [Price] >= 10 THEN 6 ELSE 10 END AS [Weight]
    FROM [P]
)
SELECT [Sku], [Name], [Price], [Weight],
    SUM([Weight]) OVER (ORDER BY [Sku] ROWS UNBOUNDED PRECEDING) - [Weight] AS [Lo]
INTO #Products FROM [W];

DECLARE @TotalWeight int = (SELECT SUM([Weight]) FROM #Products);
IF @TotalWeight IS NULL OR @PaymentCount = 0 OR @ShippingCount = 0
BEGIN
    PRINT 'Missing products, payment or shipping methods; nothing to seed.';
    ROLLBACK TRANSACTION;
    RETURN;
END;

-- 3. Customers (from contacts with an email where available).
IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
SELECT TOP (400) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N] INTO #Numbers FROM sys.all_objects;

IF OBJECT_ID('tempdb..#NewCustomers') IS NOT NULL DROP TABLE #NewCustomers;
WITH [C] AS (
    SELECT ROW_NUMBER() OVER (ORDER BY [ContactID]) AS [N], [ContactFirstName], [ContactLastName], [ContactEmail]
    FROM [OM_Contact]
    WHERE [ContactEmail] IS NOT NULL AND [ContactEmail] <> N''
)
SELECT N.[N],
    ISNULL(NULLIF(C.[ContactFirstName], N''), N'Customer') AS [FirstName],
    ISNULL(NULLIF(C.[ContactLastName], N''), CONCAT(N'Seed ', N.[N])) AS [LastName],
    ISNULL(C.[ContactEmail], CONCAT(N'customer', N.[N], N'@example.com')) AS [Email],
    NEWID() AS [Guid]
INTO #NewCustomers
FROM #Numbers N
LEFT JOIN [C] ON C.[N] = N.[N]
WHERE N.[N] <= 25;

INSERT INTO [Commerce_Customer] ([CustomerGUID], [CustomerMemberID], [CustomerCreatedWhen], [CustomerFirstName], [CustomerLastName], [CustomerEmail], [CustomerPhone])
SELECT [Guid], NULL, DATEADD(day, -130 + [N], CAST(@Today AS datetime2)), [FirstName], [LastName], [Email], NULL
FROM #NewCustomers;

IF OBJECT_ID('tempdb..#Customers') IS NOT NULL DROP TABLE #Customers;
SELECT NC.[N] - 1 AS [Idx], C.[CustomerID], NC.[FirstName], NC.[LastName], NC.[Email]
INTO #Customers
FROM #NewCustomers NC
JOIN [Commerce_Customer] C ON C.[CustomerGUID] = NC.[Guid];

DECLARE @CustomerCount int = (SELECT COUNT(*) FROM #Customers);

-- 4. Orders per day: weekdays 2-4, weekends 0-2, promo spike 40-45 days back, some empty days.
IF OBJECT_ID('tempdb..#Days') IS NOT NULL DROP TABLE #Days;
SELECT D.[DaysBack], D.[Day],
    CASE
        WHEN ABS(CHECKSUM(CONCAT(N'empty', D.[DaysBack]))) % 17 = 0 THEN 0
        WHEN D.[DaysBack] BETWEEN 40 AND 45 THEN 8 + ABS(CHECKSUM(CONCAT(N'promo', D.[DaysBack]))) % 6
        WHEN DATEDIFF(day, '19000101', D.[Day]) % 7 >= 5 THEN ABS(CHECKSUM(CONCAT(N'we', D.[DaysBack]))) % 3
        ELSE 2 + ABS(CHECKSUM(CONCAT(N'wd', D.[DaysBack]))) % 3
    END AS [OrderCount]
INTO #Days
FROM (
    SELECT [N] - 1 AS [DaysBack], DATEADD(day, -([N] - 1), @Today) AS [Day] FROM #Numbers WHERE [N] <= 120
) D;

IF OBJECT_ID('tempdb..#Orders') IS NOT NULL DROP TABLE #Orders;
SELECT
    ROW_NUMBER() OVER (ORDER BY D.[Day], N.[N]) AS [Seq],
    D.[DaysBack],
    DATEADD(minute, 8 * 60 + ABS(CHECKSUM(CONCAT(N'time', D.[DaysBack], N'-', N.[N]))) % (14 * 60), CAST(D.[Day] AS datetime2)) AS [CreatedWhen],
    ABS(CHECKSUM(CONCAT(N'status', D.[DaysBack], N'-', N.[N]))) % 100 AS [StatusRoll],
    ABS(CHECKSUM(CONCAT(N'pay', D.[DaysBack], N'-', N.[N]))) % 10 AS [PayRoll],
    ABS(CHECKSUM(CONCAT(N'ship', D.[DaysBack], N'-', N.[N]))) % 10 AS [ShipRoll],
    ABS(CHECKSUM(CONCAT(N'cust', D.[DaysBack], N'-', N.[N]))) % @CustomerCount AS [CustomerIdx],
    ABS(CHECKSUM(CONCAT(N'items', D.[DaysBack], N'-', N.[N]))) % 10 AS [ItemRoll]
INTO #Orders
FROM #Days D
JOIN #Numbers N ON N.[N] <= D.[OrderCount];

-- Order lines: 1-4 candidate lines, merged per SKU.
IF OBJECT_ID('tempdb..#Lines') IS NOT NULL DROP TABLE #Lines;
WITH [Candidates] AS (
    SELECT O.[Seq], L.[N] AS [Line],
        ABS(CHECKSUM(CONCAT(N'product', O.[Seq], N'-', L.[N]))) % @TotalWeight AS [ProductRoll],
        ABS(CHECKSUM(CONCAT(N'qty', O.[Seq], N'-', L.[N]))) % 3 AS [QtyRoll]
    FROM #Orders O
    JOIN #Numbers L ON L.[N] <= CASE WHEN O.[ItemRoll] < 4 THEN 1 WHEN O.[ItemRoll] < 7 THEN 2 WHEN O.[ItemRoll] < 9 THEN 3 ELSE 4 END
)
SELECT C.[Seq], P.[Sku], P.[Name], P.[Price],
    CAST(SUM(CASE WHEN P.[Price] < 10 THEN 1 + C.[QtyRoll] ELSE 1 END) AS decimal(19, 4)) AS [Quantity]
INTO #Lines
FROM [Candidates] C
JOIN #Products P ON C.[ProductRoll] >= P.[Lo] AND C.[ProductRoll] < P.[Lo] + P.[Weight]
GROUP BY C.[Seq], P.[Sku], P.[Name], P.[Price];

IF OBJECT_ID('tempdb..#OrderTotals') IS NOT NULL DROP TABLE #OrderTotals;
SELECT [Seq],
    SUM(ROUND([Quantity] * [Price], 2)) AS [TotalPrice],
    SUM(ROUND(ROUND([Quantity] * [Price], 2) * @TaxRate / 100, 2)) AS [TotalTax]
INTO #OrderTotals
FROM #Lines
GROUP BY [Seq];

INSERT INTO [Commerce_Order] (
    [OrderGUID], [OrderNumber], [OrderCreatedWhen], [OrderModifiedWhen], [OrderOrderStatusID],
    [OrderTotalPrice], [OrderTotalShipping], [OrderTotalTax], [OrderGrandTotal], [OrderCustomerID],
    [OrderPaymentMethodID], [OrderPaymentMethodDisplayName], [OrderShippingMethodID], [OrderShippingMethodDisplayName], [OrderShippingMethodPrice])
SELECT
    NEWID(),
    CONCAT(N'SEED-', RIGHT(CONCAT(N'00000', O.[Seq]), 5)),
    O.[CreatedWhen],
    O.[CreatedWhen],
    CASE
        WHEN O.[DaysBack] >= 10 THEN
            CASE WHEN O.[StatusRoll] < 62 THEN @Fulfilled WHEN O.[StatusRoll] < 86 THEN @Received WHEN O.[StatusRoll] < 92 THEN @Processing WHEN O.[StatusRoll] < 96 THEN @Pending ELSE @Failed END
        ELSE
            CASE WHEN O.[StatusRoll] < 20 THEN @Fulfilled WHEN O.[StatusRoll] < 45 THEN @Received WHEN O.[StatusRoll] < 70 THEN @Processing WHEN O.[StatusRoll] < 93 THEN @Pending ELSE @Failed END
    END,
    T.[TotalPrice],
    S.[Price],
    T.[TotalTax],
    T.[TotalPrice] + S.[Price] + T.[TotalTax],
    C.[CustomerID],
    P.[PaymentMethodID], P.[PaymentMethodDisplayName],
    S.[ShippingMethodID], S.[ShippingMethodDisplayName], S.[Price]
FROM #Orders O
JOIN #OrderTotals T ON T.[Seq] = O.[Seq]
JOIN #Customers C ON C.[Idx] = O.[CustomerIdx]
JOIN #Payment P ON P.[Idx] = CASE WHEN O.[PayRoll] < 7 THEN 0 ELSE 1 END % @PaymentCount
JOIN #Shipping S ON S.[Idx] = CASE WHEN O.[ShipRoll] < 7 THEN 1 ELSE 0 END % @ShippingCount;

INSERT INTO [Commerce_OrderItem] (
    [OrderItemGUID], [OrderItemOrderID], [OrderItemSKU], [OrderItemName],
    [OrderItemQuantity], [OrderItemUnitPrice], [OrderItemTotalPrice], [OrderItemTotalTax], [OrderItemTaxRate])
SELECT
    NEWID(), O.[OrderID], L.[Sku], L.[Name],
    L.[Quantity], L.[Price], ROUND(L.[Quantity] * L.[Price], 2), ROUND(ROUND(L.[Quantity] * L.[Price], 2) * @TaxRate / 100, 2), @TaxRate
FROM #Lines L
JOIN [Commerce_Order] O ON O.[OrderNumber] = CONCAT(N'SEED-', RIGHT(CONCAT(N'00000', L.[Seq]), 5));

INSERT INTO [Commerce_OrderAddress] (
    [OrderAddressGUID], [OrderAddressOrderID], [OrderAddressType], [OrderAddressFirstName], [OrderAddressLastName],
    [OrderAddressEmail], [OrderAddressLine1], [OrderAddressCity], [OrderAddressZip])
SELECT NEWID(), O.[OrderID], N'Billing', C.[FirstName], C.[LastName], C.[Email], N'1 Seed Street', N'Brno', N'60200'
FROM [Commerce_Order] O
JOIN #Customers C ON C.[CustomerID] = O.[OrderCustomerID]
WHERE O.[OrderNumber] LIKE @Pattern;

COMMIT TRANSACTION;

SELECT COUNT(*) AS [SeededOrders], SUM([OrderGrandTotal]) AS [Revenue], MIN([OrderCreatedWhen]) AS [First], MAX([OrderCreatedWhen]) AS [Last]
FROM [Commerce_Order] WHERE [OrderNumber] LIKE @Pattern;

SELECT S.[OrderStatusDisplayName], COUNT(O.[OrderID]) AS [Orders]
FROM [Commerce_OrderStatus] S
LEFT JOIN [Commerce_Order] O ON O.[OrderOrderStatusID] = S.[OrderStatusID] AND O.[OrderNumber] LIKE @Pattern
GROUP BY S.[OrderStatusDisplayName], S.[OrderStatusOrder]
ORDER BY S.[OrderStatusOrder];
