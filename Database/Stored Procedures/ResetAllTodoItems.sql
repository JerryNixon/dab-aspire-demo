CREATE PROC dbo.ResetAllTodoItems
    @isCompleted BIT = 0
AS
BEGIN
    UPDATE dbo.Todo
    SET IsCompleted = @isCompleted;
END;