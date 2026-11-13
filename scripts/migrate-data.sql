:ON ERROR EXIT
:setvar SourceDatabase "ECommerceDb"
:setvar ProductDatabase "ECommerceProducts"
:setvar UserDatabase "ECommerceUsers"
:setvar OrderDatabase "ECommerceOrders"

SET NOCOUNT ON;
SET XACT_ABORT ON;

USE [$(SourceDatabase)];

IF DB_ID(N'$(ProductDatabase)') IS NULL
    THROW 51000, 'Product database does not exist. Apply its EF migrations first.', 1;
IF DB_ID(N'$(UserDatabase)') IS NULL
    THROW 51001, 'User database does not exist. Apply its EF migrations first.', 1;
IF DB_ID(N'$(OrderDatabase)') IS NULL
    THROW 51002, 'Order database does not exist. Apply its EF migrations first.', 1;
IF OBJECT_ID(N'dbo.Carts', N'U') IS NULL OR OBJECT_ID(N'dbo.CartItems', N'U') IS NULL
    THROW 51007, 'Legacy cart tables are missing. Apply the reviewed legacy cart migration first.', 1;
IF OBJECT_ID(N'[$(ProductDatabase)].dbo.InventoryReservations', N'U') IS NULL
    THROW 51018, 'Product inventory reservation migration is missing.', 1;
IF COL_LENGTH(N'dbo.Products', N'ImageUrl') IS NULL
    THROW 51008, 'Legacy product image migration is missing.', 1;
IF COL_LENGTH(N'dbo.Users', N'Role') IS NULL
    THROW 51009, 'Legacy user-role migration is missing.', 1;
IF COL_LENGTH(N'dbo.Orders', N'CustomerName') IS NULL
   OR COL_LENGTH(N'dbo.Orders', N'ShippingAddress') IS NULL
   OR COL_LENGTH(N'dbo.Orders', N'PhoneNumber') IS NULL
    THROW 51017, 'Legacy order-shipping migration is missing.', 1;
IF COL_LENGTH(N'[$(OrderDatabase)].dbo.Orders', N'ReservationId') IS NULL
    THROW 51019, 'Order reservation migration is missing.', 1;
GO

IF EXISTS (
    SELECT 1
    FROM dbo.Inventories sourceInventory
    LEFT JOIN dbo.Products sourceProduct ON sourceProduct.Id = sourceInventory.ProductId
    WHERE sourceProduct.Id IS NULL
)
    THROW 51003, 'Source inventory contains a missing product reference.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.Orders sourceOrder
    LEFT JOIN dbo.Users sourceUser ON sourceUser.Id = sourceOrder.UserId
    WHERE sourceUser.Id IS NULL
)
    THROW 51004, 'Source orders contain a missing user reference.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.Carts sourceCart
    LEFT JOIN dbo.Users sourceUser ON sourceUser.Id = sourceCart.UserId
    WHERE sourceUser.Id IS NULL
)
    THROW 51005, 'Source carts contain a missing user reference.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.OrderItems sourceItem
    LEFT JOIN dbo.Orders sourceOrder ON sourceOrder.Id = sourceItem.OrderId
    LEFT JOIN dbo.Products sourceProduct ON sourceProduct.Id = sourceItem.ProductId
    WHERE sourceOrder.Id IS NULL OR sourceProduct.Id IS NULL
)
    THROW 51006, 'Source order items contain a missing order or product reference.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT source.Id, source.Name, source.Description, source.Price, source.Category,
               source.ImageUrl, source.IsActive, source.CreatedAt
        FROM dbo.Products source
        INNER JOIN [$(ProductDatabase)].dbo.Products target ON target.Id = source.Id
        WHERE EXISTS (
            SELECT source.Name, source.Description, source.Price, source.Category,
                   source.ImageUrl, source.IsActive, source.CreatedAt
            EXCEPT
            SELECT target.Name, target.Description, target.Price, target.Category,
                   target.ImageUrl, target.IsActive, target.CreatedAt
        )
    )
        THROW 51010, 'Conflicting product IDs exist in the target database.', 1;

    SET IDENTITY_INSERT [$(ProductDatabase)].dbo.Products ON;
    INSERT INTO [$(ProductDatabase)].dbo.Products
        (Id, Name, Description, Price, Category, ImageUrl, IsActive, CreatedAt)
    SELECT source.Id, source.Name, source.Description, source.Price, source.Category,
           source.ImageUrl, source.IsActive, source.CreatedAt
    FROM dbo.Products source
    WHERE NOT EXISTS (
        SELECT 1
        FROM [$(ProductDatabase)].dbo.Products target
        WHERE target.Id = source.Id
    );
    SET IDENTITY_INSERT [$(ProductDatabase)].dbo.Products OFF;

    IF EXISTS (
        SELECT source.Id, source.ProductId, source.Quantity, source.UpdatedAt
        FROM dbo.Inventories source
        INNER JOIN [$(ProductDatabase)].dbo.Inventories target ON target.Id = source.Id
        WHERE EXISTS (
            SELECT source.ProductId, source.Quantity, source.UpdatedAt
            EXCEPT
            SELECT target.ProductId, target.Quantity, target.UpdatedAt
        )
    )
        THROW 51011, 'Conflicting inventory IDs exist in the target database.', 1;

    SET IDENTITY_INSERT [$(ProductDatabase)].dbo.Inventories ON;
    INSERT INTO [$(ProductDatabase)].dbo.Inventories (Id, ProductId, Quantity, UpdatedAt)
    SELECT source.Id, source.ProductId, source.Quantity, source.UpdatedAt
    FROM dbo.Inventories source
    WHERE NOT EXISTS (
        SELECT 1
        FROM [$(ProductDatabase)].dbo.Inventories target
        WHERE target.Id = source.Id
    );
    SET IDENTITY_INSERT [$(ProductDatabase)].dbo.Inventories OFF;

    IF EXISTS (
        SELECT source.Id, source.FirstName, source.LastName, source.Email,
               source.PasswordHash, source.Role, source.CreatedAt
        FROM dbo.Users source
        INNER JOIN [$(UserDatabase)].dbo.Users target ON target.Id = source.Id
        WHERE EXISTS (
            SELECT source.FirstName, source.LastName, source.Email,
                   source.PasswordHash, source.Role, source.CreatedAt
            EXCEPT
            SELECT target.FirstName, target.LastName, target.Email,
                   target.PasswordHash, target.Role, target.CreatedAt
        )
    )
        THROW 51012, 'Conflicting user IDs exist in the target database.', 1;

    SET IDENTITY_INSERT [$(UserDatabase)].dbo.Users ON;
    INSERT INTO [$(UserDatabase)].dbo.Users
        (Id, FirstName, LastName, Email, PasswordHash, Role, CreatedAt)
    SELECT source.Id, source.FirstName, source.LastName, source.Email,
           source.PasswordHash, source.Role, source.CreatedAt
    FROM dbo.Users source
    WHERE NOT EXISTS (
        SELECT 1 FROM [$(UserDatabase)].dbo.Users target WHERE target.Id = source.Id
    );
    SET IDENTITY_INSERT [$(UserDatabase)].dbo.Users OFF;

    IF EXISTS (
        SELECT source.Id, source.UserId, source.OrderDate, source.TotalAmount,
               source.CustomerName, source.ShippingAddress, source.PhoneNumber,
               CASE source.Status
                   WHEN 0 THEN N'Pending'
                   WHEN 1 THEN N'Confirmed'
                   WHEN 2 THEN N'Processing'
                   WHEN 3 THEN N'Shipped'
                   WHEN 4 THEN N'Delivered'
                   WHEN 5 THEN N'Cancelled'
                   ELSE N'Unknown'
               END,
               CASE WHEN source.Status = 0
                   THEN CONVERT(uniqueidentifier,
                       HASHBYTES('MD5', CONCAT(N'legacy-order-', source.Id)))
                   ELSE NULL
               END
        FROM dbo.Orders source
        INNER JOIN [$(OrderDatabase)].dbo.Orders target ON target.Id = source.Id
        WHERE EXISTS (
            SELECT source.UserId, source.OrderDate, source.TotalAmount,
                   source.CustomerName, source.ShippingAddress, source.PhoneNumber,
                   CASE source.Status
                       WHEN 0 THEN N'Pending'
                       WHEN 1 THEN N'Confirmed'
                       WHEN 2 THEN N'Processing'
                       WHEN 3 THEN N'Shipped'
                       WHEN 4 THEN N'Delivered'
                       WHEN 5 THEN N'Cancelled'
                       ELSE N'Unknown'
                   END,
                   CASE WHEN source.Status = 0
                       THEN CONVERT(uniqueidentifier,
                           HASHBYTES('MD5', CONCAT(N'legacy-order-', source.Id)))
                       ELSE NULL
                   END
            EXCEPT
            SELECT target.UserId, target.OrderDate, target.TotalAmount,
                   target.CustomerName, target.ShippingAddress, target.PhoneNumber,
                   target.Status, target.ReservationId
        )
    )
        THROW 51013, 'Conflicting order IDs exist in the target database.', 1;

    SET IDENTITY_INSERT [$(OrderDatabase)].dbo.Orders ON;
    INSERT INTO [$(OrderDatabase)].dbo.Orders
        (Id, UserId, OrderDate, TotalAmount, CustomerName, ShippingAddress, PhoneNumber, Status, ReservationId)
    SELECT source.Id, source.UserId, source.OrderDate, source.TotalAmount,
           source.CustomerName, source.ShippingAddress, source.PhoneNumber,
           CASE source.Status
               WHEN 0 THEN N'Pending'
               WHEN 1 THEN N'Confirmed'
               WHEN 2 THEN N'Processing'
               WHEN 3 THEN N'Shipped'
               WHEN 4 THEN N'Delivered'
               WHEN 5 THEN N'Cancelled'
               ELSE N'Unknown'
           END,
           CASE WHEN source.Status = 0
               THEN CONVERT(uniqueidentifier,
                   HASHBYTES('MD5', CONCAT(N'legacy-order-', source.Id)))
               ELSE NULL
           END
    FROM dbo.Orders source
    WHERE NOT EXISTS (
        SELECT 1 FROM [$(OrderDatabase)].dbo.Orders target WHERE target.Id = source.Id
    );
    SET IDENTITY_INSERT [$(OrderDatabase)].dbo.Orders OFF;

    IF EXISTS (
        SELECT reservation.ReservationId, reservation.ProductId, reservation.Quantity
        FROM (
            SELECT CONVERT(uniqueidentifier,
                       HASHBYTES('MD5', CONCAT(N'legacy-order-', orderRecord.Id))) AS ReservationId,
                   item.ProductId,
                   SUM(item.Quantity) AS Quantity
            FROM dbo.Orders orderRecord
            INNER JOIN dbo.OrderItems item ON item.OrderId = orderRecord.Id
            WHERE orderRecord.Status = 0
            GROUP BY orderRecord.Id, item.ProductId
        ) reservation
        INNER JOIN [$(ProductDatabase)].dbo.InventoryReservations target
            ON target.ReservationId = reservation.ReservationId
            AND target.ProductId = reservation.ProductId
        WHERE target.Quantity <> reservation.Quantity OR target.Status <> N'Reserved'
    )
        THROW 51029, 'Conflicting legacy inventory reservations exist in the target database.', 1;

    INSERT INTO [$(ProductDatabase)].dbo.InventoryReservations
        (ReservationId, ProductId, Quantity, Status, CreatedAt)
    SELECT reservation.ReservationId, reservation.ProductId, reservation.Quantity,
           N'Reserved', reservation.OrderDate
    FROM (
        SELECT CONVERT(uniqueidentifier,
                   HASHBYTES('MD5', CONCAT(N'legacy-order-', orderRecord.Id))) AS ReservationId,
               orderRecord.OrderDate,
               item.ProductId,
               SUM(item.Quantity) AS Quantity
        FROM dbo.Orders orderRecord
        INNER JOIN dbo.OrderItems item ON item.OrderId = orderRecord.Id
        WHERE orderRecord.Status = 0
        GROUP BY orderRecord.Id, orderRecord.OrderDate, item.ProductId
    ) reservation
    WHERE NOT EXISTS (
        SELECT 1
        FROM [$(ProductDatabase)].dbo.InventoryReservations target
        WHERE target.ReservationId = reservation.ReservationId
          AND target.ProductId = reservation.ProductId
    );

    IF EXISTS (
        SELECT source.Id, source.OrderId, source.ProductId, source.Quantity, source.UnitPrice,
               product.Name, product.ImageUrl
        FROM dbo.OrderItems source
        INNER JOIN dbo.Products product ON product.Id = source.ProductId
        INNER JOIN [$(OrderDatabase)].dbo.OrderItems target ON target.Id = source.Id
        WHERE EXISTS (
            SELECT source.OrderId, source.ProductId, source.Quantity, source.UnitPrice,
                   product.Name, product.ImageUrl
            EXCEPT
            SELECT target.OrderId, target.ProductId, target.Quantity, target.UnitPrice,
                   target.ProductName, target.ProductImageUrl
        )
    )
        THROW 51014, 'Conflicting order item IDs exist in the target database.', 1;

    SET IDENTITY_INSERT [$(OrderDatabase)].dbo.OrderItems ON;
    INSERT INTO [$(OrderDatabase)].dbo.OrderItems
        (Id, OrderId, ProductId, ProductName, ProductImageUrl, Quantity, UnitPrice)
    SELECT source.Id, source.OrderId, source.ProductId, product.Name, product.ImageUrl,
           source.Quantity, source.UnitPrice
    FROM dbo.OrderItems source
    INNER JOIN dbo.Products product ON product.Id = source.ProductId
    WHERE NOT EXISTS (
        SELECT 1
        FROM [$(OrderDatabase)].dbo.OrderItems target
        WHERE target.Id = source.Id
    );
    SET IDENTITY_INSERT [$(OrderDatabase)].dbo.OrderItems OFF;

    IF EXISTS (
        SELECT source.Id, source.UserId
        FROM dbo.Carts source
        INNER JOIN [$(OrderDatabase)].dbo.Carts target ON target.Id = source.Id
        WHERE source.UserId <> target.UserId
    )
        THROW 51015, 'Conflicting cart IDs exist in the target database.', 1;

    SET IDENTITY_INSERT [$(OrderDatabase)].dbo.Carts ON;
    INSERT INTO [$(OrderDatabase)].dbo.Carts (Id, UserId)
    SELECT source.Id, source.UserId
    FROM dbo.Carts source
    WHERE NOT EXISTS (
        SELECT 1 FROM [$(OrderDatabase)].dbo.Carts target WHERE target.Id = source.Id
    );
    SET IDENTITY_INSERT [$(OrderDatabase)].dbo.Carts OFF;

    IF EXISTS (
        SELECT source.Id, source.CartId, source.ProductId, source.Quantity
        FROM dbo.CartItems source
        INNER JOIN [$(OrderDatabase)].dbo.CartItems target ON target.Id = source.Id
        WHERE EXISTS (
            SELECT source.CartId, source.ProductId, source.Quantity
            EXCEPT
            SELECT target.CartId, target.ProductId, target.Quantity
        )
    )
        THROW 51016, 'Conflicting cart item IDs exist in the target database.', 1;

    SET IDENTITY_INSERT [$(OrderDatabase)].dbo.CartItems ON;
    INSERT INTO [$(OrderDatabase)].dbo.CartItems (Id, CartId, ProductId, Quantity)
    SELECT source.Id, source.CartId, source.ProductId, source.Quantity
    FROM dbo.CartItems source
    WHERE NOT EXISTS (
        SELECT 1 FROM [$(OrderDatabase)].dbo.CartItems target WHERE target.Id = source.Id
    );
    SET IDENTITY_INSERT [$(OrderDatabase)].dbo.CartItems OFF;

    IF (SELECT COUNT_BIG(*) FROM dbo.Products) <>
       (SELECT COUNT_BIG(*) FROM [$(ProductDatabase)].dbo.Products)
        THROW 51020, 'Product row counts do not match after copy.', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.Inventories) <>
       (SELECT COUNT_BIG(*) FROM [$(ProductDatabase)].dbo.Inventories)
        THROW 51021, 'Inventory row counts do not match after copy.', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.Users) <>
       (SELECT COUNT_BIG(*) FROM [$(UserDatabase)].dbo.Users)
        THROW 51022, 'User row counts do not match after copy.', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.Orders) <>
       (SELECT COUNT_BIG(*) FROM [$(OrderDatabase)].dbo.Orders)
        THROW 51023, 'Order row counts do not match after copy.', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.OrderItems) <>
       (SELECT COUNT_BIG(*) FROM [$(OrderDatabase)].dbo.OrderItems)
        THROW 51024, 'Order item row counts do not match after copy.', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.Carts) <>
       (SELECT COUNT_BIG(*) FROM [$(OrderDatabase)].dbo.Carts)
        THROW 51025, 'Cart row counts do not match after copy.', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.CartItems) <>
       (SELECT COUNT_BIG(*) FROM [$(OrderDatabase)].dbo.CartItems)
        THROW 51026, 'Cart item row counts do not match after copy.', 1;
    IF (
        SELECT COUNT_BIG(*)
        FROM (
            SELECT orderRecord.Id, item.ProductId
            FROM dbo.Orders orderRecord
            INNER JOIN dbo.OrderItems item ON item.OrderId = orderRecord.Id
            WHERE orderRecord.Status = 0
            GROUP BY orderRecord.Id, item.ProductId
        ) expectedReservations
    ) <> (
        SELECT COUNT_BIG(*)
        FROM [$(ProductDatabase)].dbo.InventoryReservations
    )
        THROW 51030, 'Pending-order inventory reservation counts do not match.', 1;

    IF EXISTS (
        SELECT 1
        FROM [$(ProductDatabase)].dbo.Inventories inventory
        LEFT JOIN [$(ProductDatabase)].dbo.Products product
            ON product.Id = inventory.ProductId
        WHERE product.Id IS NULL
    )
        THROW 51027, 'Copied inventory contains a missing product reference.', 1;

    IF EXISTS (
        SELECT 1
        FROM [$(OrderDatabase)].dbo.Orders orderRecord
        LEFT JOIN [$(UserDatabase)].dbo.Users userRecord
            ON userRecord.Id = orderRecord.UserId
        WHERE userRecord.Id IS NULL
    )
        THROW 51028, 'Copied orders contain a missing user reference.', 1;

    COMMIT TRANSACTION;

    SELECT N'Products' AS EntityName,
           (SELECT COUNT_BIG(*) FROM dbo.Products) AS SourceCount,
           (SELECT COUNT_BIG(*) FROM [$(ProductDatabase)].dbo.Products) AS TargetCount
    UNION ALL SELECT N'Inventories',
           (SELECT COUNT_BIG(*) FROM dbo.Inventories),
           (SELECT COUNT_BIG(*) FROM [$(ProductDatabase)].dbo.Inventories)
    UNION ALL SELECT N'Users',
           (SELECT COUNT_BIG(*) FROM dbo.Users),
           (SELECT COUNT_BIG(*) FROM [$(UserDatabase)].dbo.Users)
    UNION ALL SELECT N'Orders',
           (SELECT COUNT_BIG(*) FROM dbo.Orders),
           (SELECT COUNT_BIG(*) FROM [$(OrderDatabase)].dbo.Orders)
    UNION ALL SELECT N'OrderItems',
           (SELECT COUNT_BIG(*) FROM dbo.OrderItems),
           (SELECT COUNT_BIG(*) FROM [$(OrderDatabase)].dbo.OrderItems)
    UNION ALL SELECT N'Carts',
           (SELECT COUNT_BIG(*) FROM dbo.Carts),
           (SELECT COUNT_BIG(*) FROM [$(OrderDatabase)].dbo.Carts)
    UNION ALL SELECT N'CartItems',
           (SELECT COUNT_BIG(*) FROM dbo.CartItems),
           (SELECT COUNT_BIG(*) FROM [$(OrderDatabase)].dbo.CartItems);
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
