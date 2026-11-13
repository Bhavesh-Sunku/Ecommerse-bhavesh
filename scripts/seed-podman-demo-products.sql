SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'ECommerceProducts'
    THROW 51100, 'This demo seed is restricted to the ECommerceProducts database.', 1;

DECLARE @Catalog TABLE
(
    Name nvarchar(200) NOT NULL,
    Description nvarchar(1000) NOT NULL,
    Price decimal(18, 2) NOT NULL,
    Category nvarchar(100) NOT NULL
);

INSERT INTO @Catalog (Name, Description, Price, Category)
VALUES
    (N'Wireless Headphones', N'Comfortable wireless headphones with clear everyday sound.', 1999.00, N'Electronics'),
    (N'Portable Bluetooth Speaker', N'Compact speaker for music at home or on the go.', 2999.00, N'Electronics'),
    (N'Fast Charging Power Bank', N'Portable power bank for charging phones and small devices.', 3999.00, N'Electronics'),
    (N'USB-C Hub', N'Multiport USB-C hub for laptop connectivity.', 4999.00, N'Electronics'),
    (N'Wireless Mouse', N'Ergonomic wireless mouse for work and study.', 5999.00, N'Electronics'),
    (N'1080p Webcam', N'Full HD webcam for calls, classes, and streaming.', 6999.00, N'Electronics'),
    (N'Mechanical Keyboard', N'Compact mechanical keyboard with tactile keys.', 7999.00, N'Electronics'),
    (N'Smart Fitness Band', N'Fitness band for activity and everyday wellness tracking.', 8999.00, N'Electronics'),
    (N'Portable SSD 1TB', N'Portable solid-state storage for files and backups.', 9999.00, N'Electronics'),
    (N'Noise Cancelling Earbuds', N'True wireless earbuds with active noise reduction.', 10999.00, N'Electronics'),

    (N'Classic Cotton T-Shirt', N'Soft cotton everyday T-shirt with a comfortable fit.', 999.00, N'Fashion'),
    (N'Everyday Denim Jeans', N'Classic denim jeans designed for daily wear.', 1999.00, N'Fashion'),
    (N'Lightweight Hoodie', N'Layer-friendly hoodie for casual outings.', 2999.00, N'Fashion'),
    (N'Casual Running Shoes', N'Lightweight shoes for walks and daily activities.', 3999.00, N'Fashion'),
    (N'Canvas Backpack', N'Everyday backpack with roomy storage compartments.', 4999.00, N'Fashion'),
    (N'Leather Belt', N'Versatile belt with a simple polished buckle.', 5999.00, N'Fashion'),
    (N'Classic Analog Watch', N'Clean analog watch for everyday outfits.', 6999.00, N'Fashion'),
    (N'Sport Sunglasses', N'Lightweight sunglasses for outdoor use.', 7999.00, N'Fashion'),
    (N'Warm Winter Jacket', N'Insulated jacket for cool-weather comfort.', 8999.00, N'Fashion'),
    (N'Everyday Travel Duffel', N'Durable duffel bag for short trips and gym visits.', 9999.00, N'Fashion'),

    (N'Ceramic Dinner Plate Set', N'Set of durable ceramic plates for everyday dining.', 1999.00, N'Home & Kitchen'),
    (N'Insulated Water Bottle', N'Reusable bottle that keeps drinks at temperature.', 2999.00, N'Home & Kitchen'),
    (N'Nonstick Frying Pan', N'Easy-clean frying pan for everyday cooking.', 3999.00, N'Home & Kitchen'),
    (N'Cotton Bedsheet Set', N'Soft cotton bedding set for a comfortable night.', 4999.00, N'Home & Kitchen'),
    (N'LED Desk Lamp', N'Adjustable desk lamp for reading and focused work.', 5999.00, N'Home & Kitchen'),
    (N'Compact Storage Organizer', N'Space-saving organizer for household essentials.', 6999.00, N'Home & Kitchen'),
    (N'French Press Coffee Maker', N'Simple coffee press for rich brewed coffee.', 7999.00, N'Home & Kitchen'),
    (N'Four Slice Toaster', N'Countertop toaster with adjustable browning settings.', 8999.00, N'Home & Kitchen'),
    (N'Air Fryer 4L', N'Compact air fryer for convenient everyday meals.', 9999.00, N'Home & Kitchen'),
    (N'Cordless Hand Blender', N'Hand blender for soups, sauces, and smoothies.', 10999.00, N'Home & Kitchen'),

    (N'Everyday Science Guide', N'Accessible introduction to practical science concepts.', 999.00, N'Books'),
    (N'Beginner Python Handbook', N'Friendly guide to programming fundamentals with Python.', 1999.00, N'Books'),
    (N'World History Illustrated', N'Illustrated overview of major events across world history.', 2999.00, N'Books'),
    (N'Creative Writing Workbook', N'Guided exercises for building a daily writing habit.', 3999.00, N'Books'),
    (N'Personal Finance Basics', N'Practical introduction to budgeting and saving.', 4999.00, N'Books'),
    (N'Home Gardening Guide', N'Helpful guide to growing plants in home spaces.', 5999.00, N'Books'),
    (N'Quick Vegetarian Recipes', N'Collection of simple vegetarian meals for busy days.', 6999.00, N'Books'),
    (N'Introduction to Astronomy', N'Beginner-friendly tour of stars, planets, and space.', 7999.00, N'Books'),
    (N'Photography Field Manual', N'Practical tips for composing and capturing photographs.', 8999.00, N'Books'),
    (N'Mindful Living Journal', N'Guided journal for reflection and mindful routines.', 9999.00, N'Books'),

    (N'Yoga Mat', N'Non-slip exercise mat for yoga and stretching.', 1999.00, N'Sports & Outdoors'),
    (N'Adjustable Dumbbell Pair', N'Compact dumbbell pair for home strength training.', 2999.00, N'Sports & Outdoors'),
    (N'Insulated Hiking Bottle', N'Durable bottle for hikes and outdoor activities.', 3999.00, N'Sports & Outdoors'),
    (N'Badminton Racket Set', N'Pair of recreational rackets for casual games.', 4999.00, N'Sports & Outdoors'),
    (N'Football Training Ball', N'Durable ball for practice and recreational play.', 5999.00, N'Sports & Outdoors'),
    (N'Camping Sleeping Bag', N'Packable sleeping bag for weekend camping trips.', 6999.00, N'Sports & Outdoors'),
    (N'Foldable Trekking Poles', N'Adjustable trekking poles for trail support.', 7999.00, N'Sports & Outdoors'),
    (N'Lightweight Fitness Kettlebell', N'Kettlebell for strength and conditioning workouts.', 8999.00, N'Sports & Outdoors'),
    (N'Outdoor Camping Lantern', N'Rechargeable lantern for campsites and emergencies.', 9999.00, N'Sports & Outdoors'),
    (N'Compact Camping Tent', N'Easy-to-carry tent for outdoor weekend trips.', 10999.00, N'Sports & Outdoors');

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @UpdatedProducts TABLE (ProductId int NOT NULL);

    UPDATE product
    SET
        product.Name = catalog.Name,
        product.Description = catalog.Description,
        product.Price = catalog.Price,
        product.Category = catalog.Category,
        product.ImageUrl = N'',
        product.IsActive = 1
    OUTPUT inserted.Id INTO @UpdatedProducts (ProductId)
    FROM dbo.Products AS product
    CROSS JOIN @Catalog AS catalog
    WHERE product.Id = 970001
      AND product.Name = N'Podman Demo Product'
      AND catalog.Name = N'Wireless Headphones'
      AND catalog.Category = N'Electronics';

    IF EXISTS (SELECT 1 FROM @UpdatedProducts)
    BEGIN
        UPDATE dbo.Inventories
        SET Quantity = 10,
            UpdatedAt = SYSUTCDATETIME()
        WHERE ProductId = 970001;
    END;

    INSERT INTO dbo.Products
        (Name, Description, Price, Category, ImageUrl, IsActive, CreatedAt)
    SELECT
        catalog.Name,
        catalog.Description,
        catalog.Price,
        catalog.Category,
        N'',
        1,
        SYSUTCDATETIME()
    FROM @Catalog AS catalog
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Products AS product
        WHERE product.Name = catalog.Name
          AND product.Category = catalog.Category
    );

    UPDATE inventory
    SET
        inventory.Quantity = 10,
        inventory.UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Inventories AS inventory
    INNER JOIN dbo.Products AS product ON product.Id = inventory.ProductId
    INNER JOIN @Catalog AS catalog
        ON catalog.Name = product.Name
       AND catalog.Category = product.Category;

    INSERT INTO dbo.Inventories (ProductId, Quantity, UpdatedAt)
    SELECT product.Id, 10, SYSUTCDATETIME()
    FROM dbo.Products AS product
    INNER JOIN @Catalog AS catalog
        ON catalog.Name = product.Name
       AND catalog.Category = product.Category
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Inventories AS inventory
        WHERE inventory.ProductId = product.Id
    );

    IF EXISTS
    (
        SELECT catalog.Category
        FROM @Catalog AS catalog
        LEFT JOIN dbo.Products AS product
            ON product.Name = catalog.Name
           AND product.Category = catalog.Category
        LEFT JOIN dbo.Inventories AS inventory
            ON inventory.ProductId = product.Id
        GROUP BY catalog.Category
        HAVING COUNT(DISTINCT product.Id) < 10
            OR MIN(inventory.Quantity) <> 10
            OR MAX(inventory.Quantity) <> 10
    )
        THROW 51101, 'Demo catalog validation failed: expected 10 products per category with stock 10.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    product.Category,
    COUNT(*) AS ProductCount,
    MIN(inventory.Quantity) AS MinimumStock,
    MAX(inventory.Quantity) AS MaximumStock,
    MIN(product.Price) AS MinimumPrice,
    MAX(product.Price) AS MaximumPrice,
    SUM(CASE WHEN product.Price % 1000 = 999 THEN 0 ELSE 1 END) AS InvalidPriceSuffixes
FROM dbo.Products AS product
INNER JOIN dbo.Inventories AS inventory
    ON inventory.ProductId = product.Id
WHERE product.Category IN
    (N'Electronics', N'Fashion', N'Home & Kitchen', N'Books', N'Sports & Outdoors')
GROUP BY product.Category
ORDER BY product.Category;
