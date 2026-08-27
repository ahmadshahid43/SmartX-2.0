using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// Cross-origin access is only needed when the SPA is hosted on a DIFFERENT origin than the API
// (e.g. `ng serve` in development, or a split CDN deployment). In the recommended production
// setup the container serves the built SPA at the same origin, so no origins are configured and
// the policy is an inert no-op. Never use AllowAnyOrigin here — credentials/bearer tokens flow.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .ToArray();

            return new BadRequestObjectResult(new ApiErrorResponse(
                false,
                "VALIDATION_ERROR",
                "One or more validation errors occurred.",
                errors));
        };
    });
builder.Services.AddOmniBusinessFoundation(builder.Configuration, builder.Environment);
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "Bearer",
        In = ParameterLocation.Header,
        Description = "Enter the access token as: Bearer {your token}"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});

var app = builder.Build();

app.UseExceptionHandler();

// Serve the built Angular SPA at the same origin as the API when its files are present in the
// content root's wwwroot (production container image). On the laptop/dev install wwwroot is empty,
// so these are no-ops and the "/" route below still redirects to Swagger.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("frontend");
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "OmniBusiness Foundation API");
    options.RoutePrefix = "swagger";
});
app.MapOpenApi();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");
app.MapControllers();

// SPA client-side routing fallback: unmatched non-API GETs return index.html when the SPA is
// deployed (see UseStaticFiles above). Returns 404 harmlessly when no SPA is present.
app.MapFallbackToFile("index.html");

app.Run();
