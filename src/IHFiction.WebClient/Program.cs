using IHFiction.WebClient.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (must be first)
builder.AddServiceDefaults();

// Add feature-based service registrations
builder.AddCoreWebServices();
builder.AddPersistenceServices();
builder.AddSecurityServices();
builder.AddApplicationServices();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseWebClientPipeline(builder.Configuration);

await app.RunAsync();

