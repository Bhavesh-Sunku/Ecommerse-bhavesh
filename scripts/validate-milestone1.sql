SET NOCOUNT ON;

SELECT DB_NAME() AS DatabaseName, SYSDATETIMEOFFSET() AS CheckedAt;

SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT required.TableName,
       CASE WHEN actual.object_id IS NULL THEN 'MISSING' ELSE 'PRESENT' END AS TableStatus
FROM (VALUES
    ('Products'),
    ('Users'),
    ('Carts'),
    ('CartItems'),
    ('Orders'),
    ('OrderItems'),
    ('Inventories'),
    ('Payments')
) AS required(TableName)
LEFT JOIN sys.tables AS actual
    ON actual.name = required.TableName
   AND SCHEMA_NAME(actual.schema_id) = 'dbo'
ORDER BY required.TableName;

SELECT 'Products' AS TableName, COUNT_BIG(*) AS RowsInTable FROM dbo.Products
UNION ALL SELECT 'Users', COUNT_BIG(*) FROM dbo.Users
UNION ALL SELECT 'Carts', COUNT_BIG(*) FROM dbo.Carts
UNION ALL SELECT 'CartItems', COUNT_BIG(*) FROM dbo.CartItems
UNION ALL SELECT 'Orders', COUNT_BIG(*) FROM dbo.Orders
UNION ALL SELECT 'OrderItems', COUNT_BIG(*) FROM dbo.OrderItems
UNION ALL SELECT 'Inventories', COUNT_BIG(*) FROM dbo.Inventories
UNION ALL SELECT 'Payments', COUNT_BIG(*) FROM dbo.Payments;

SELECT p.Id AS ProductId, p.Name
FROM dbo.Products AS p
LEFT JOIN dbo.Inventories AS i ON i.ProductId = p.Id
WHERE i.ProductId IS NULL
ORDER BY p.Id;

SELECT 'Products with non-positive price' AS CheckName, COUNT_BIG(*) AS ProblemCount
FROM dbo.Products
WHERE Price <= 0
UNION ALL
SELECT 'Negative inventory', COUNT_BIG(*)
FROM dbo.Inventories
WHERE Quantity < 0
UNION ALL
SELECT 'Duplicate normalized user emails', COUNT_BIG(*)
FROM (
    SELECT LOWER(LTRIM(RTRIM(Email))) AS NormalizedEmail
    FROM dbo.Users
    GROUP BY LOWER(LTRIM(RTRIM(Email)))
    HAVING COUNT_BIG(*) > 1
) AS duplicateEmails
UNION ALL
SELECT 'Order items with missing order or product', COUNT_BIG(*)
FROM dbo.OrderItems AS oi
LEFT JOIN dbo.Orders AS o ON o.Id = oi.OrderId
LEFT JOIN dbo.Products AS p ON p.Id = oi.ProductId
WHERE o.Id IS NULL OR p.Id IS NULL
UNION ALL
SELECT 'Cart items with missing cart or product', COUNT_BIG(*)
FROM dbo.CartItems AS ci
LEFT JOIN dbo.Carts AS c ON c.Id = ci.CartId
LEFT JOIN dbo.Products AS p ON p.Id = ci.ProductId
WHERE c.Id IS NULL OR p.Id IS NULL
UNION ALL
SELECT 'Orders with missing user', COUNT_BIG(*)
FROM dbo.Orders AS o
LEFT JOIN dbo.Users AS u ON u.Id = o.UserId
WHERE u.Id IS NULL;

DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
