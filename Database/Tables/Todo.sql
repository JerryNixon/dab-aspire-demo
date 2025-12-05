CREATE TABLE dbo.Todo
(
    Id INT PRIMARY KEY IDENTITY,
    Title NVARCHAR(500) NOT NULL,
    IsCompleted BIT NOT NULL DEFAULT 0,
    CategoryId INT NOT NULL,
    CONSTRAINT FK_Todo_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Category(Id)
)