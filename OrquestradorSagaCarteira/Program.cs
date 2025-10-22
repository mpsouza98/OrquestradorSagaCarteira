using OrquestradorSagaCarteira.Aplicacao.Acoes;
using OrquestradorSagaCarteira.Aplicacao.Observadores;
using OrquestradorSagaCarteira.Aplicacao.Servicos;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;
using Calculadora.Core.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configuração do DbConnectionFactory
builder.Services.AddSingleton<DbConnectionFactory>();

// Registrar Repositories (Scoped para garantir uma conexão por request)
builder.Services.AddScoped<ISagaRepository, SagaRepository>();
builder.Services.AddScoped<IOperacaoRepository, OperacaoRepository>();
builder.Services.AddScoped<IBarreiraRepository, BarreiraRepository>();
builder.Services.AddScoped<ICotacaoRepository, CotacaoRepository>();
builder.Services.AddScoped<IEventoBarreiraRepository, EventoBarreiraRepository>();
builder.Services.AddScoped<IEventoAutocallRepository, EventoAutocallRepository>();
builder.Services.AddScoped<ILiquidacaoRepository, LiquidacaoRepository>();

// Registrar serviços da Calculadora
builder.Services.AddScoped<CalculadoraBarreira>();
builder.Services.AddScoped<CalculadoraAutocall>();

// Registrar serviços de aplicação
builder.Services.AddScoped<IOrquestradorSaga, OrquestradorSaga>();
builder.Services.AddSingleton<IPublicadorEventos, PublicadorEventos>();

// Registrar todas as ações da saga como Transient
builder.Services.AddTransient<IAcaoSaga, AcaoVerificarBarreira>();
builder.Services.AddTransient<IAcaoSaga, AcaoPersistirBarreira>();
builder.Services.AddTransient<IAcaoSaga, AcaoNotificarBarreiraAtingida>();
builder.Services.AddTransient<IAcaoSaga, AcaoAgregarCesta>();
builder.Services.AddTransient<IAcaoSaga, AcaoVerificarAutoCall>();
builder.Services.AddTransient<IAcaoSaga, AcaoPersistirAutoCall>();
builder.Services.AddTransient<IAcaoSaga, AcaoNotificarAutoCall>();
builder.Services.AddTransient<IAcaoSaga, AcaoAgendarDesfazimentoOperacao>();

// Registrar observadores como Singleton (precisam persistir durante toda a aplicação)
builder.Services.AddSingleton<ObservadorBarreira>();
builder.Services.AddSingleton<ObservadorAutoCall>();
builder.Services.AddSingleton<ObservadorDesfazimentoOperacao>();

// Configurar controllers e Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "Orquestrador Saga Autocall", 
        Version = "v1",
        Description = "API para gestão de operações Autocall usando padrão Saga Orquestrado"
    });
});

var app = builder.Build();

// Configurar pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Saga Autocall v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("🚀 Aplicação Orquestrador Saga Autocall iniciada");

app.Run();
