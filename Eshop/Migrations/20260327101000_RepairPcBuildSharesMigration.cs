using Eshop.Repository;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eshop.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260327101000_RepairPcBuildSharesMigration")]
    public partial class RepairPcBuildSharesMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[PcBuildShares]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [PcBuildShares] (
                        [Id] int NOT NULL IDENTITY,
                        [ShareCode] nvarchar(16) NULL,
                        [PcBuildId] int NOT NULL,
                        [SenderUserId] nvarchar(450) NOT NULL,
                        [ReceiverUserId] nvarchar(450) NOT NULL,
                        [Note] nvarchar(500) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [OpenedAt] datetime2 NULL,
                        CONSTRAINT [PK_PcBuildShares] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_PcBuildShares_AspNetUsers_ReceiverUserId] FOREIGN KEY ([ReceiverUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_PcBuildShares_AspNetUsers_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_PcBuildShares_PcBuilds_PcBuildId] FOREIGN KEY ([PcBuildId]) REFERENCES [PcBuilds] ([Id]) ON DELETE CASCADE
                    );
                END;

                IF OBJECT_ID(N'[PcBuildShares]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE name = N'IX_PcBuildShares_PcBuildId'
                          AND object_id = OBJECT_ID(N'[PcBuildShares]')
                    )
                    BEGIN
                        CREATE INDEX [IX_PcBuildShares_PcBuildId] ON [PcBuildShares] ([PcBuildId]);
                    END;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE name = N'IX_PcBuildShares_ReceiverUserId_CreatedAt'
                          AND object_id = OBJECT_ID(N'[PcBuildShares]')
                    )
                    BEGIN
                        CREATE INDEX [IX_PcBuildShares_ReceiverUserId_CreatedAt] ON [PcBuildShares] ([ReceiverUserId], [CreatedAt]);
                    END;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE name = N'IX_PcBuildShares_SenderUserId'
                          AND object_id = OBJECT_ID(N'[PcBuildShares]')
                    )
                    BEGIN
                        CREATE INDEX [IX_PcBuildShares_SenderUserId] ON [PcBuildShares] ([SenderUserId]);
                    END;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE name = N'IX_PcBuildShares_ShareCode'
                          AND object_id = OBJECT_ID(N'[PcBuildShares]')
                    )
                    BEGIN
                        CREATE UNIQUE INDEX [IX_PcBuildShares_ShareCode]
                        ON [PcBuildShares] ([ShareCode])
                        WHERE [ShareCode] IS NOT NULL;
                    END;
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
