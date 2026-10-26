using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadLetterPermissionsToSuperAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
EXEC sys.sp_set_session_context @key = N'IsSuperAdmin', @value = 1;

IF EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [Id] = '00000000-0000-0000-0000-000000000001')
BEGIN
    DECLARE @Permissions NVARCHAR(MAX);
    SELECT @Permissions = [PermissionsJson] FROM [dbo].[Roles] WHERE [Id] = '00000000-0000-0000-0000-000000000001';

    IF @Permissions IS NULL OR @Permissions = '' OR @Permissions = '[]'
    BEGIN
        SET @Permissions = '[""Permissions.Platform.DeadLetter.View"",""Permissions.Platform.DeadLetter.Reprocess""]';
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM OPENJSON(@Permissions) WHERE [value] = 'Permissions.Platform.DeadLetter.View')
        BEGIN
            SET @Permissions = JSON_MODIFY(@Permissions, 'append $', 'Permissions.Platform.DeadLetter.View');
        END
        IF NOT EXISTS (SELECT 1 FROM OPENJSON(@Permissions) WHERE [value] = 'Permissions.Platform.DeadLetter.Reprocess')
        BEGIN
            SET @Permissions = JSON_MODIFY(@Permissions, 'append $', 'Permissions.Platform.DeadLetter.Reprocess');
        END
    END

    UPDATE [dbo].[Roles]
    SET [PermissionsJson] = @Permissions
    WHERE [Id] = '00000000-0000-0000-0000-000000000001';
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
EXEC sys.sp_set_session_context @key = N'IsSuperAdmin', @value = 1;

IF EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [Id] = '00000000-0000-0000-0000-000000000001')
BEGIN
    DECLARE @Permissions NVARCHAR(MAX);
    SELECT @Permissions = [PermissionsJson] FROM [dbo].[Roles] WHERE [Id] = '00000000-0000-0000-0000-000000000001';

    IF @Permissions IS NOT NULL AND @Permissions <> ''
    BEGIN
        SET @Permissions = REPLACE(@Permissions, ',""Permissions.Platform.DeadLetter.View""', '');
        SET @Permissions = REPLACE(@Permissions, '""Permissions.Platform.DeadLetter.View"",', '');
        SET @Permissions = REPLACE(@Permissions, '""Permissions.Platform.DeadLetter.View""', '');
        SET @Permissions = REPLACE(@Permissions, ',""Permissions.Platform.DeadLetter.Reprocess""', '');
        SET @Permissions = REPLACE(@Permissions, '""Permissions.Platform.DeadLetter.Reprocess"",', '');
        SET @Permissions = REPLACE(@Permissions, '""Permissions.Platform.DeadLetter.Reprocess""', '');

        UPDATE [dbo].[Roles]
        SET [PermissionsJson] = @Permissions
        WHERE [Id] = '00000000-0000-0000-0000-000000000001';
    END
END
");
        }
    }
}
