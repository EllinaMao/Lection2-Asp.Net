using Microsoft.Data.SqlClient;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Reflection;
using System.Text;
using WebApplication1;
using WebApplication1.Models;

/*
 На основе рассмотренного примера с пользователями, реализовать следующие возможности:
1) Добавление пользователя.
2) Удаления пользователя.
3) Редактирование пользователя.
4) Поиск пользователей по имени.
5) Сортировка пользователей на основе выпадающего списка (по имени или возрасту).
6) (Необязательный пункт, но можно, если было мало) Реализовать пагинацию. Внизу таблицы отображать кнопки, с помощью которых можно выполнять навигацию по пользователям, за раз выводить по 10 человек на страницу.
 */

var builder = WebApplication.CreateBuilder();
var app = builder.Build();
//Получаем сервис IConfiguration, через свойство Services 
var configurationService = app.Services.GetService<IConfiguration>();

//С помощью индексатора обращаемся к нужной строке подключения.
//Необходимо указать полный путь к секции, через двоеточие
string? connectionString = configurationService?["ConnectionStrings:DefaultConnection"];
int pageSize = configurationService?.GetValue<int>("PageSize", 10) ?? 10;

var columns = new Dictionary<string, string>
{
    ["Id"] = "ID Пользователя",
    ["Name"] = "Полное имя",
    ["Age"] = "Возраст"
};
List<Button> buttons = new List<Button>
        {
            new Button{Link = "/editUser", Text="Edit", Class="btn btn-warning m-1"},
            new Button{Action = "/removeUser", Text="Delete", Class="btn btn-danger m-1"},
        };


app.Run(async context =>
{
    var response = context.Response;
    var request = context.Request;
    var path = request.Path;
    response.ContentType = "text/html; charset=utf-8";



    //При переходе на главную страницу, считываем всех пользователей
    if (path == Routes.Home && request.Method == "GET")
    {   
        //params   
        string? search = request.Query["search"];


        //sort params
        string? sortBy = request.Query["sortBy"].FirstOrDefault() ?? "Id";
        if (!columns.ContainsKey(sortBy)) sortBy = "Id";

        string[] allowedSort = { "Id", "Name", "Age" };

        sortBy = allowedSort.Contains(sortBy, StringComparer.OrdinalIgnoreCase)
                 ? sortBy
                 : "Id";

        //direction of sorting
        string dir = request.Query["dir"].FirstOrDefault() ?? "asc";
        dir = dir.ToLower() == "desc" ? "desc" : "asc";

        //pagination
        int page = int.TryParse(request.Query["page"], out var p) && p > 0 ? p : 1;
        int totalUsers = 0;


        List<User> users = new List<User>();
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            using (SqlCommand countCmd = new SqlCommand(SqlQueries.CountUsers, connection))
            {
                countCmd.Parameters.AddWithValue("@search", $"%{search}%");
                totalUsers = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
            }

            string sql = SqlQueries.GetUsers(sortBy, dir);
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@search", $"%{search}%");
                command.Parameters.AddWithValue("@skip", (page - 1) * pageSize);
                command.Parameters.AddWithValue("@take", pageSize);

                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(new User(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
                    }
                }
            }

        }
        string controlsHtml = GetControlsSection(search, sortBy, dir);
        string tableHtml = BuildHtmlTable(users, buttons);
        string paginationHtml = GetPaginationSection(totalUsers, page, pageSize, search, sortBy, dir);

        await response.WriteAsync(GenerateHtmlPage(controlsHtml + tableHtml + paginationHtml, "User Management"));
    }

    else if (path == Routes.AddUser && request.Method == "GET")
    {
        await response.WriteAsync(GenerateHtmlPage(GetUserCreateForm(), "Add New User"));
    }

    else if (path == "/addUser" && request.Method == "POST")
    {
        var form = await request.ReadFormAsync();
        using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            var cmd = new SqlCommand(SqlQueries.InsertUser, conn);
            cmd.Parameters.AddWithValue("@n", form["name"].ToString());
            cmd.Parameters.AddWithValue("@a", int.Parse(form["age"]));

            await cmd.ExecuteScalarAsync();
        }
        response.Redirect(Routes.Home);
    }

    else if (path == Routes.EditUser && request.Method == "GET")
    {
        int id = int.Parse(request.Query["id"]);
        User user = null;
        using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var cmd = new SqlCommand(SqlQueries.GetById,conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) user = new User(r.GetInt32(0), r.GetString(1), r.GetInt32(2));
        }

        if (user != null)
            await response.WriteAsync(GenerateHtmlPage(GetUserUpdateForm(user), "Edit User"));
        else
            response.Redirect(Routes.Home);
    }

    else if (path == Routes.EditUser && request.Method == "POST")
    {
        var form = await request.ReadFormAsync();
        using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var cmd = new SqlCommand(SqlQueries.UpdateUser, conn);
            cmd.Parameters.AddWithValue("@id", int.Parse(form["id"]));
            cmd.Parameters.AddWithValue("@n", form["name"].ToString());
            cmd.Parameters.AddWithValue("@a", int.Parse(form["age"]));
            await cmd.ExecuteNonQueryAsync();
        }
        response.Redirect(Routes.Home);
    }

    else if (path == "/deleteUser" && request.Method == "POST")
    {
        var form = await request.ReadFormAsync();
        using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            var cmd = new SqlCommand(SqlQueries.DeleteUser, conn);
            cmd.Parameters.AddWithValue("@id", int.Parse(form["id"]));
            await cmd.ExecuteNonQueryAsync();
        }
        response.Redirect(Routes.Home);
    }

    else
    {
        response.StatusCode = 404;
        await response.WriteAsync("Page Not Found");
    }
});
app.Run();
static string GetControlsSection(string search, string sortBy, string dir)
{
    return $"""
        <form method='GET' class='row g-2 mb-3 bg-light p-3 rounded border'>
            <div class='col-md-4'>
                <input type='text' name='search' class='form-control' placeholder='Поиск...' value='{search}'>
            </div>
            <div class='col-md-3'>
                <select name='sortBy' class='form-select'>
                    <option value='Id' {(sortBy == "Id" ? "selected" : "")}>ID</option>
                    <option value='Name' {(sortBy == "Name" ? "selected" : "")}>Имя</option>
                    <option value='Age' {(sortBy == "Age" ? "selected" : "")}>Возраст</option>
                </select>
            </div>
            <div class='col-md-2'>
                <select name='dir' class='form-select'>
                    <option value='asc' {(dir == "asc" ? "selected" : "")}>По возр.</option>
                    <option value='desc' {(dir == "desc" ? "selected" : "")}>По убыв.</option>
                </select>
            </div>
            <div class='col-md-2'>
                <button type='submit' class='btn btn-primary w-100'>Найти</button>
            </div>
             <div class='col-md-1'>
                <a href='{Routes.AddUser}' class='btn btn-success w-100'>Add</a>
            </div>
        </form>
    """;
}

static string BuildHtmlTable(IEnumerable<User> collection, List<Button> buttons)
{
    StringBuilder tableHtml = new StringBuilder();
    tableHtml.Append("<table class='table table-striped table-hover mt-3'>");
    tableHtml.Append("<thead class='table-dark'><tr><th>Id</th><th>Name</th><th>Age</th><th>Actions</th></tr></thead><tbody>");

    foreach (User item in collection)
    {
        tableHtml.Append("<tr>");
        tableHtml.Append($"<td>{item.Id}</td>");
        tableHtml.Append($"<td>{item.Name}</td>");
        tableHtml.Append($"<td>{item.Age}</td>");
        tableHtml.Append("<td><div class='d-flex gap-2'>");

        foreach (var btn in buttons)
        {
            if (!string.IsNullOrEmpty(btn.Link))
            {
                tableHtml.Append($"<a href='{btn.Link}?id={item.Id}' class='{btn.Class}'>{btn.Text}</a>");
            }
            else if (!string.IsNullOrEmpty(btn.Action))
            {
                string onclick = btn.Text == "Delete" ? "onclick='return confirm(\"Вы уверены?\")'" : "";
                tableHtml.Append($@"
                    <form action='{btn.Action}' method='{btn.Method}' style='margin:0'>
                        <input type='hidden' name='id' value='{item.Id}' />
                        <button class='{btn.Class}' {onclick}>{btn.Text}</button>
                    </form>");
            }
        }
        tableHtml.Append("</div></td>");
        tableHtml.Append("</tr>");
    }
    tableHtml.Append("</tbody></table>");
    return tableHtml.ToString();
}

static string GetUserCreateForm()
{
    return $"""
        <div class="card p-4 shadow-sm">
            <form action="{Routes.AddUser}" method="post">
                <div class="mb-3">
                    <label for="name" class="form-label">Name:</label>
                    <input type="text" name="name" id="name" class="form-control" required />
                </div>
                <div class="mb-3">
                    <label for="age" class="form-label">Age:</label>
                    <input type="number" name="age" id="age" class="form-control" required />
                </div>
                <button type="submit" class="btn btn-primary">Submit</button>
                <a href="{Routes.Home}" class="btn btn-secondary ms-2">Cancel</a>
            </form>
        </div>
    """;
}

static string GetUserUpdateForm(User user)
{
    return $"""
        <div class="card p-4 shadow-sm">
            <h4 class="mb-3">Edit User #{user.Id}</h4>
            <form action="{Routes.EditUser}" method="post">
                <input type="hidden" name="id" value="{user.Id}" />
                <div class="mb-3">
                    <label for="name" class="form-label">Name:</label>
                    <input type="text" name="name" class="form-control" value="{user.Name}" required />
                </div>
                <div class="mb-3">
                    <label for="age" class="form-label">Age:</label>
                    <input type="number" name="age" class="form-control" value="{user.Age}" required />
                </div>
                <button type="submit" class="btn btn-warning">Update</button>
                <a href="{Routes.Home}" class="btn btn-secondary ms-2">Cancel</a>
            </form>
        </div>
    """;
}

static string GetPaginationSection(int totalUsers, int currentPage, int pageSize, string search, string sort, string dir)
{
    int totalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
    if (totalPages <= 1) return "";

    string query = $"&search={search ?? ""}&sortBy={sort ?? "Id"}&dir={dir ?? "asc"}";
    string prevDisabled = currentPage <= 1 ? "disabled" : "";
    string nextDisabled = currentPage >= totalPages ? "disabled" : "";

    return $"""
        <nav class="mt-4">
            <ul class="pagination justify-content-center">
                <li class="page-item {prevDisabled}">
                    <a class="page-link" href="/?page={currentPage - 1}{query}">Previous</a>
                </li>
                <li class="page-item disabled">
                    <span class="page-link">Page {currentPage} of {totalPages}</span>
                </li>
                <li class="page-item {nextDisabled}">
                    <a class="page-link" href="/?page={currentPage + 1}{query}">Next</a>
                </li>
            </ul>
        </nav>
    """;
}

static string GenerateHtmlPage(string body, string header)
{
    return $"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0-alpha3/dist/css/bootstrap.min.css" rel="stylesheet">
            <title>{header}</title>
        </head>
        <body>
        <div class="container">
            <h2 class="d-flex justify-content-center mt-3">{header}</h2>
            <div class="mt-4"></div>
            {body}
        </div>
        </body>
        </html>
        """;
}
class Button
{
    public string Text { get; set; }
    public bool IsForm { get; set; }
    public string? Link { get; set; }
    public string? Class { get; set; } = "btn btn-submit";
    public string? Action { get; set; }
    public string? Method { get; set; } = "post";
}

