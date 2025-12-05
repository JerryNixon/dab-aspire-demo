IF NOT EXISTS (SELECT 1 FROM dbo.Category)
BEGIN
    INSERT dbo.Category (Name)
    VALUES
        ('Home'),
        ('Work'),
        ('School'),
        ('Personal');
END
GO

-- Seed Todos only if none exist
IF NOT EXISTS (SELECT 1 FROM dbo.Todo)
BEGIN
    INSERT dbo.Todo (Title, IsCompleted, CategoryId)
    VALUES
        ('Walk the dog', 0, 1),
        ('Feed the fish', 0, 2),
        ('Clean the cat', 1, 1);
END
GO