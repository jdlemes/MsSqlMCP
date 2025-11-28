using MsSqlMCP.Interfaces;
using MsSqlMCP.Services;
using Xunit;

namespace MsSqlMCP.Tests;

/// <summary>
/// Unit tests for ReadOnlySqlQueryValidator to ensure security.
/// </summary>
public class ReadOnlySqlQueryValidatorTests
{
    private readonly ISqlQueryValidator _validator = new ReadOnlySqlQueryValidator();

    #region Valid SELECT Queries

    [Theory]
    [InlineData("SELECT * FROM Users")]
    [InlineData("select id, name from products")]
    [InlineData("SELECT TOP 10 * FROM Orders ORDER BY CreatedDate DESC")]
    [InlineData("SELECT COUNT(*) FROM Customers WHERE IsActive = 1")]
    [InlineData("SELECT a.*, b.Name FROM TableA a JOIN TableB b ON a.Id = b.AId")]
    public void Validate_SimpleSelectQueries_ReturnsValid(string query)
    {
        var result = _validator.Validate(query);
        Assert.True(result.IsValid, $"Query should be valid: {query}. Error: {result.ErrorMessage}");
    }

    [Theory]
    [InlineData("WITH cte AS (SELECT 1 as Id) SELECT * FROM cte")]
    [InlineData("WITH Orders_CTE AS (SELECT * FROM Orders) SELECT * FROM Orders_CTE")]
    public void Validate_CTEQueries_ReturnsValid(string query)
    {
        var result = _validator.Validate(query);
        Assert.True(result.IsValid, $"CTE query should be valid: {query}. Error: {result.ErrorMessage}");
    }

    [Theory]
    [InlineData("SELECT * FROM Users WHERE UpdatedDate > '2023-01-01'")] // Column named UpdatedDate
    [InlineData("SELECT DeletedAt, InsertedBy FROM AuditLog")] // Column names containing keywords
    [InlineData("SELECT * FROM CREATE_LOG")] // Table name containing keyword
    public void Validate_QueriesWithKeywordLikeNames_ReturnsValid(string query)
    {
        var result = _validator.Validate(query);
        Assert.True(result.IsValid, $"Query with keyword-like names should be valid: {query}. Error: {result.ErrorMessage}");
    }

    #endregion

    #region Invalid DML Queries - All should be blocked

    [Theory]
    [InlineData("INSERT INTO Users (Name) VALUES ('Test')")]
    [InlineData("insert into products values (1, 'test', 10.99)")]
    public void Validate_InsertQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"INSERT query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("UPDATE Users SET Name = 'Test' WHERE Id = 1")]
    [InlineData("update products set price = 20.00")]
    public void Validate_UpdateQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"UPDATE query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("DELETE FROM Users WHERE Id = 1")]
    [InlineData("delete from orders")]
    public void Validate_DeleteQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"DELETE query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("TRUNCATE TABLE Users")]
    [InlineData("truncate table logs")]
    public void Validate_TruncateQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"TRUNCATE query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("MERGE INTO Target USING Source ON Target.Id = Source.Id WHEN MATCHED THEN UPDATE SET Name = Source.Name")]
    public void Validate_MergeQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"MERGE query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region Invalid DDL Queries

    [Theory]
    [InlineData("DROP TABLE Users")]
    [InlineData("drop database TestDb")]
    [InlineData("DROP INDEX IX_Users_Name ON Users")]
    public void Validate_DropQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"DROP query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("ALTER TABLE Users ADD Email VARCHAR(255)")]
    [InlineData("alter table products drop column description")]
    public void Validate_AlterQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"ALTER query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("CREATE TABLE NewTable (Id INT PRIMARY KEY)")]
    [InlineData("create index IX_Test on Users(Name)")]
    [InlineData("CREATE PROCEDURE sp_Test AS SELECT 1")]
    public void Validate_CreateQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"CREATE query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region Invalid Execution Queries

    [Theory]
    [InlineData("EXEC sp_DeleteAllUsers")]
    [InlineData("execute sp_DropDatabase")]
    [InlineData("EXEC('DELETE FROM Users')")]
    public void Validate_ExecQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"EXEC query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region SQL Injection Attempts

    [Theory]
    [InlineData("SELECT * FROM Users; DELETE FROM Users")]
    [InlineData("SELECT 1; DROP TABLE Users")]
    [InlineData("SELECT * FROM Users; INSERT INTO Logs VALUES ('hacked')")]
    public void Validate_MultipleStatements_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"Multiple statements should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("SELECT * FROM Users WHERE Id = 1; --\nDELETE FROM Users")]
    public void Validate_CommentInjection_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"Comment injection should be blocked: {query}");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyOrNullQueries_ReturnsInvalid(string? query)
    {
        var result = _validator.Validate(query!);
        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("GRANT SELECT ON Users TO TestUser")]
    [InlineData("REVOKE ALL ON Database TO TestUser")]
    [InlineData("DENY INSERT ON Users TO TestUser")]
    public void Validate_DCLQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"DCL query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("BACKUP DATABASE TestDb TO DISK = 'C:\\backup.bak'")]
    [InlineData("RESTORE DATABASE TestDb FROM DISK = 'C:\\backup.bak'")]
    public void Validate_BackupRestoreQueries_ReturnsInvalid(string query)
    {
        var result = _validator.Validate(query);
        Assert.False(result.IsValid, $"BACKUP/RESTORE query should be blocked: {query}");
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion
}
