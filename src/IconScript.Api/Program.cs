using IconScript;

// API HTTP del compilador (sección 6.2 del plan).
//   POST /compile   { "source": "..." }  -> CompileResult en JSON
//   GET  /examples                       -> [{ name, source }] de la carpeta examples/
var builder = WebApplication.CreateBuilder(args);

const string WebOrigin = "web";
builder.Services.AddCors(o => o.AddPolicy(WebOrigin, p =>
    p.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors(WebOrigin);

// Tiempo máximo por compilación: la VM revisa el token de cancelación periódicamente.
var timeout = TimeSpan.FromSeconds(2);

app.MapPost("/compile", (CompileRequest req) =>
{
    using var cts = new CancellationTokenSource(timeout);
    return IconCompiler.Compile(req.Source ?? "", cts.Token);
});

app.MapGet("/examples", () =>
{
    var dir = FindExamplesDir(app.Configuration["ExamplesDir"], app.Environment.ContentRootPath);
    if (dir == null) return Results.Ok(Array.Empty<Example>());
    var list = Directory.GetFiles(dir, "*.icon")
        .OrderBy(f => f, StringComparer.Ordinal)
        .Select(f => new Example(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f)))
        .ToList();
    return Results.Ok(list);
});

app.MapGet("/", () => "IconScript API: POST /compile, GET /examples");

app.Run();

// Busca examples/ subiendo desde la carpeta del proyecto (o usa la configuración ExamplesDir).
static string? FindExamplesDir(string? configured, string start)
{
    if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured)) return configured;
    for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
    {
        var candidate = Path.Combine(d.FullName, "examples");
        if (Directory.Exists(candidate)) return candidate;
    }
    return null;
}

record CompileRequest(string? Source);
record Example(string Name, string Source);
