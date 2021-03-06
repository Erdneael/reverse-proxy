using ReverseProxyApplication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

//Use the Proxy as Middleware to catch all request
app.UseMiddleware<ReverseProxyMiddleware>();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
