using Microsoft.EntityFrameworkCore;
using OrquestradorSagaCarteira.Aplicacao.Acoes;
using OrquestradorSagaCarteira.Aplicacao.Observadores;
using OrquestradorSagaCarteira.Aplicacao.Servicos;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;

var builder = WebApplication.CreateBuilder(args);

// Configuração do banco de dados MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<CarteiraDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Registrar serviços
builder.Services.AddScoped<IOrquestradorSaga, OrquestradorSaga>();
builder.Services.AddSingleton<IPublicadorEventos, PublicadorEventos>();

// Registrar todas as ações da saga
builder.Services.AddScoped<AcaoProcessarCotacao>();
builder.Services.AddScoped<AcaoCalcularMtmRendaFixa>();
builder.Services.AddScoped<AcaoCalcularMtmRendaVariavel>();
builder.Services.AddScoped<AcaoConsolidarMtm>();
builder.Services.AddScoped<AcaoCalcularValorizacaoContabil>();
builder.Services.AddScoped<AcaoAtualizarPosicaoCliente>();
builder.Services.AddScoped<AcaoProcessarSplit>();
builder.Services.AddScoped<AcaoProcessarInsplit>();
builder.Services.AddScoped<AcaoAjustarPosicoes>();
builder.Services.AddScoped<AcaoVerificarBarreira>();
builder.Services.AddScoped<AcaoProcessarAtingimentoBarreira>();
builder.Services.AddScoped<AcaoIniciarLiquidacao>();
builder.Services.AddScoped<AcaoCalcularValorLiquidacao>();
builder.Services.AddScoped<AcaoLiquidarPosicoes>();
builder.Services.AddScoped<AcaoEncerrarCoe>();
builder.Services.AddScoped<AcaoLancarContabilidade>();
builder.Services.AddScoped<AcaoRealizarAjusteContabil>();

// Registrar observadores
builder.Services.AddSingleton<ObservadorCotacao>();
builder.Services.AddSingleton<ObservadorMtm>();
builder.Services.AddSingleton<ObservadorBarreira>();
builder.Services.AddSingleton<ObservadorEventoCorporativo>();

// Configurar controllers e Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Orquestrador Saga Carteira COE", 
        Version = "v1",
        Description = "API para gestão de eventos de carteira COE usando padrão Saga"
    });
});

var app = builder.Build();

// Configurar pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Saga Carteira COE v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Inicializar observadores
using (var scope = app.Services.CreateScope())
{
    var publicador = scope.ServiceProvider.GetRequiredService<IPublicadorEventos>();
    var observadorCotacao = scope.ServiceProvider.GetRequiredService<ObservadorCotacao>();
    var observadorMtm = scope.ServiceProvider.GetRequiredService<ObservadorMtm>();
    var observadorBarreira = scope.ServiceProvider.GetRequiredService<ObservadorBarreira>();
    var observadorEventoCorporativo = scope.ServiceProvider.GetRequiredService<ObservadorEventoCorporativo>();

    // Inscrever observadores nos tópicos
    await publicador.InscreverObservadorAsync("topico.cotacoes", observadorCotacao);
    await publicador.InscreverObservadorAsync("topico.mtm", observadorMtm);
    await publicador.InscreverObservadorAsync("topico.barreiras", observadorBarreira);
    await publicador.InscreverObservadorAsync("topico.eventos-corporativos", observadorEventoCorporativo);

    app.Logger.LogInformation("Observadores inscritos nos tópicos");
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}