-- --------------------------------------------------
-- TinyCrm database schema
-- Equivalent of the EDMX designer's "Generate Database from Model"
-- output for TinyCrmModel.edmx, targeting SQL Server 2012+ / LocalDB.
--
-- The application normally creates and seeds the database via the
-- EF CreateDatabaseIfNotExists initializer at startup; this script
-- is provided for manual provisioning.
-- --------------------------------------------------

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'TinyCrm')
BEGIN
    CREATE DATABASE [TinyCrm];
END
GO

USE [TinyCrm];
GO

SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[dbo].[FK_Interactions_Customers]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[Interactions] DROP CONSTRAINT [FK_Interactions_Customers];
IF OBJECT_ID(N'[dbo].[Interactions]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Interactions];
IF OBJECT_ID(N'[dbo].[Customers]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Customers];
IF OBJECT_ID(N'[dbo].[Users]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Users];
GO

CREATE TABLE [dbo].[Customers] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Company] nvarchar(150) NULL,
    [Email] nvarchar(150) NULL,
    [Phone] nvarchar(50) NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetime NOT NULL,
    [LastInteractionDate] datetime NULL
);
ALTER TABLE [dbo].[Customers]
ADD CONSTRAINT [PK_dbo.Customers] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

CREATE TABLE [dbo].[Interactions] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [CustomerId] int NOT NULL,
    [Type] int NOT NULL,
    [Subject] nvarchar(200) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [InteractionDate] datetime NOT NULL,
    [CreatedAt] datetime NOT NULL
);
ALTER TABLE [dbo].[Interactions]
ADD CONSTRAINT [PK_dbo.Interactions] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

CREATE TABLE [dbo].[Users] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Username] nvarchar(50) NOT NULL,
    [PasswordHash] nvarchar(64) NOT NULL,
    [DisplayName] nvarchar(100) NOT NULL
);
ALTER TABLE [dbo].[Users]
ADD CONSTRAINT [PK_dbo.Users] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

ALTER TABLE [dbo].[Interactions] ADD CONSTRAINT [FK_Interactions_Customers]
    FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
    ON DELETE CASCADE;
GO
