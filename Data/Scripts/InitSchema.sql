-- =============================================
-- EcommScrapperBenchmark Database Schema
-- Idempotent: safe to run multiple times
-- =============================================

-- Provider configurations with API keys
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProviderConfig')
BEGIN
    CREATE TABLE ProviderConfig (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ProviderName NVARCHAR(50) NOT NULL,
        ApiKey NVARCHAR(500),
        BaseUrl NVARCHAR(500),
        AuthType NVARCHAR(50) DEFAULT 'Bearer',
        IsActive BIT DEFAULT 1,
        RateLimitPerMinute INT DEFAULT 10,
        CreatedOn DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedOn DATETIME2 DEFAULT GETUTCDATE()
    );

    CREATE UNIQUE INDEX UX_ProviderConfig_ProviderName ON ProviderConfig(ProviderName);
END
GO

-- Test products (URLs + UPC codes per platform)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TestProduct')
BEGIN
    CREATE TABLE TestProduct (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Platform NVARCHAR(50) NOT NULL,
        ProductUrl NVARCHAR(1000) NOT NULL,
        ProductName NVARCHAR(500),
        UpcCode NVARCHAR(50),
        ExpectedPrice DECIMAL(10,2),
        ExpectedBrand NVARCHAR(200),
        IsActive BIT DEFAULT 1,
        CreatedOn DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedOn DATETIME2 DEFAULT GETUTCDATE()
    );
END
GO

-- Benchmark run (a single execution session)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BenchmarkRun')
BEGIN
    CREATE TABLE BenchmarkRun (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        RunGuid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        StartedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CompletedAt DATETIME2,
        Status NVARCHAR(50) DEFAULT 'Running',
        TotalRequests INT DEFAULT 0,
        SuccessfulRequests INT DEFAULT 0,
        FailedRequests INT DEFAULT 0,
        Notes NVARCHAR(MAX)
    );

    CREATE UNIQUE INDEX UX_BenchmarkRun_RunGuid ON BenchmarkRun(RunGuid);
END
GO

-- Individual benchmark results (one per provider x product x run)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BenchmarkResult')
BEGIN
    CREATE TABLE BenchmarkResult (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        RunId INT NOT NULL,
        ProviderName NVARCHAR(50) NOT NULL,
        Platform NVARCHAR(50) NOT NULL,
        TestProductId INT NOT NULL,
        ProductUrl NVARCHAR(1000),

        -- Performance metrics
        HttpStatusCode INT,
        ResponseTimeMs BIGINT,
        IsSuccess BIT DEFAULT 0,
        ErrorMessage NVARCHAR(MAX),

        -- Extracted data (normalized fields)
        ExtractedTitle NVARCHAR(500),
        ExtractedPrice DECIMAL(10,2),
        ExtractedCurrency NVARCHAR(10),
        ExtractedBrand NVARCHAR(200),
        ExtractedUpc NVARCHAR(50),
        ExtractedAvailability NVARCHAR(100),
        ExtractedImageUrl NVARCHAR(1000),
        ExtractedDescription NVARCHAR(MAX),
        ExtractedRating DECIMAL(3,2),
        ExtractedReviewCount INT,

        -- Raw response (for debugging)
        RawResponseJson NVARCHAR(MAX),
        RawResponseSizeBytes BIGINT,

        -- Quality scores (computed post-extraction)
        CompletenessScore DECIMAL(5,2),
        AccuracyScore DECIMAL(5,2),
        StructureScore DECIMAL(5,2),
        OverallQualityScore DECIMAL(5,2),

        CreatedOn DATETIME2 DEFAULT GETUTCDATE(),

        CONSTRAINT FK_BenchmarkResult_Run FOREIGN KEY (RunId) REFERENCES BenchmarkRun(Id),
        CONSTRAINT FK_BenchmarkResult_Product FOREIGN KEY (TestProductId) REFERENCES TestProduct(Id)
    );

    CREATE INDEX IX_BenchmarkResult_RunId ON BenchmarkResult(RunId);
    CREATE INDEX IX_BenchmarkResult_ProviderName ON BenchmarkResult(ProviderName);
    CREATE INDEX IX_BenchmarkResult_Platform ON BenchmarkResult(Platform);
END
GO

-- =============================================
-- Seed default provider configurations
-- =============================================
IF NOT EXISTS (SELECT 1 FROM ProviderConfig)
BEGIN
    INSERT INTO ProviderConfig (ProviderName, BaseUrl, AuthType, RateLimitPerMinute, IsActive) VALUES
    ('BrightData', 'https://api.brightdata.com/datasets/v3/scrape?amazon=gd_l7q7dkf244hwjntr0&walmart=gd_l95fol7l1ru6rlo116&homedepot=gd_lmusivh019i7g97q2n', 'Bearer', 10, 0),
    ('Zyte', 'https://api.zyte.com/v1/extract', 'Basic', 10, 0),
    ('Oxylabs', 'https://realtime.oxylabs.io/v1/queries', 'Basic', 10, 0),
    ('Decodo', 'https://scraper-api.decodo.com/v2/scrape', 'Basic', 10, 0),
    ('Nimbleway', 'https://api.webit.live/api/v1/realtime/web', 'Bearer', 10, 0),
    ('WebScrapingAPI', 'https://api.webscrapingapi.com/v1', 'QueryParam', 10, 0),
    ('NetNut', 'https://api.netnut.io/api/v1/scrape', 'Bearer', 10, 0),
    ('ScrapingBee', 'https://app.scrapingbee.com/api/v1/', 'QueryParam', 10, 0),
    ('ScraperAPI', 'https://api.scraperapi.com', 'QueryParam', 10, 0),
    ('ScrapingDog', 'https://api.scrapingdog.com', 'QueryParam', 10, 0),
    ('Infatica', 'https://api.infatica.io/scrape', 'Bearer', 10, 0),
    ('Rayobyte', 'https://api.rayobyte.com/scrape', 'Bearer', 10, 0);
END
GO

-- =============================================
-- Seed sample test products
-- =============================================
IF NOT EXISTS (SELECT 1 FROM TestProduct)
BEGIN
    -- Amazon products
    INSERT INTO TestProduct (Platform, ProductUrl, ProductName, UpcCode, ExpectedBrand) VALUES
    ('Amazon', 'https://www.amazon.com/dp/B0BSHF7WHW', 'Apple AirPods Pro (2nd Gen)', '194253415367', 'Apple'),
    ('Amazon', 'https://www.amazon.com/dp/B0CHX2DJKX', 'Sony WH-1000XM5 Headphones', '027242923577', 'Sony'),
    ('Amazon', 'https://www.amazon.com/dp/B0BDJ279KF', 'Kindle Paperwhite (11th Gen)', '840268920401', 'Amazon'),
    ('Amazon', 'https://www.amazon.com/dp/B09V3KXJPB', 'Apple Watch SE (2nd Gen)', '194253363354', 'Apple'),
    ('Amazon', 'https://www.amazon.com/dp/B0BDHWDR12', 'JBL Tune 510BT Headphones', '050036388542', 'JBL');

    -- Home Depot products
    INSERT INTO TestProduct (Platform, ProductUrl, ProductName, UpcCode, ExpectedBrand) VALUES
    ('HomeDepot', 'https://www.homedepot.com/p/Milwaukee-M18-FUEL-18V-Lithium-Ion-Brushless-Cordless-1-2-in-Hammer-Drill-Driver-Tool-Only-2904-20/316498565', 'Milwaukee M18 FUEL Hammer Drill', '045242577521', 'Milwaukee'),
    ('HomeDepot', 'https://www.homedepot.com/p/DEWALT-20V-MAX-Cordless-Drill-Impact-Driver-Power-Tool-Combo-Kit-with-2-Batteries-Charger-DCK240C2/203504180', 'DEWALT 20V MAX Combo Kit', '885911460491', 'DEWALT'),
    ('HomeDepot', 'https://www.homedepot.com/p/RYOBI-ONE-18V-Cordless-6-Tool-Combo-Kit-with-2-Batteries-Charger-and-Bag-P1819/309659455', 'RYOBI ONE+ 18V 6-Tool Combo', '046396036407', 'RYOBI'),
    ('HomeDepot', 'https://www.homedepot.com/p/Husky-Mechanics-Tool-Set-270-Piece-H270MTS/309713709', 'Husky 270-Piece Mechanics Set', '045242652204', 'Husky'),
    ('HomeDepot', 'https://www.homedepot.com/p/Milwaukee-PACKOUT-22-in-Rolling-Modular-Tool-Box-48-22-8426/305816337', 'Milwaukee PACKOUT Rolling Box', '045242348398', 'Milwaukee');

    -- Walmart products
    INSERT INTO TestProduct (Platform, ProductUrl, ProductName, UpcCode, ExpectedBrand) VALUES
    ('Walmart', 'https://www.walmart.com/ip/Apple-AirPods-Pro-2nd-Generation/1752657021', 'Apple AirPods Pro (2nd Gen)', '194253415367', 'Apple'),
    ('Walmart', 'https://www.walmart.com/ip/PlayStation-5-Console-Slim/5089412012', 'PlayStation 5 Console Slim', '711719568544', 'Sony'),
    ('Walmart', 'https://www.walmart.com/ip/Nintendo-Switch-OLED-Model-w-White-Joy-Con/910582148', 'Nintendo Switch OLED', '045496883386', 'Nintendo'),
    ('Walmart', 'https://www.walmart.com/ip/Dyson-V15-Detect-Cordless-Vacuum/1460165766', 'Dyson V15 Detect', '885609028873', 'Dyson'),
    ('Walmart', 'https://www.walmart.com/ip/Keurig-K-Supreme-Single-Serve-K-Cup-Pod-Coffee-Maker-Black/832498471', 'Keurig K-Supreme Coffee Maker', '611247397664', 'Keurig');
END
GO
