namespace WebApplication1
{
    public static class SqlQueries
    {
        public const string InsertUser = "INSERT INTO Users (Name, Age) VALUES (@n, @a); SELECT SCOPE_IDENTITY();";
        public const string UpdateUser = "UPDATE Users SET Name=@n, Age=@a WHERE Id=@id";
        public const string DeleteUser = "DELETE FROM Users WHERE Id=@id";
        public const string GetById = "SELECT Id, Name, Age FROM Users WHERE Id=@id";
        public const string CountUsers = "SELECT COUNT(*) FROM Users WHERE Name LIKE @search";

        public static string GetUsers(string sortBy, string dir)
        {
            return $@"SELECT Id, Name, Age FROM Users 
                  WHERE Name LIKE @search 
                  ORDER BY {sortBy} {dir} 
                  OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
        }

    }
}
