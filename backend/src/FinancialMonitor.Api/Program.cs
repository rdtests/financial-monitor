var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Run();

// Makes the top-level Program class visible to WebApplicationFactory<Program> in the test project.
public partial class Program;
