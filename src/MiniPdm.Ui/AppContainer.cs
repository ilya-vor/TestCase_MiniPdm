using Jab;
using MiniPdm.Application;
using MiniPdm.Application.Cad;
using MiniPdm.Application.Import;
using MiniPdm.Application.Persistence;
using MiniPdm.Application.Structure;
using MiniPdm.Cad;
using MiniPdm.Infrastructure;
using MiniPdm.Infrastructure.Database;
using MiniPdm.Infrastructure.Persistence;
using MiniPdm.Ui.ViewModels;

namespace MiniPdm.Ui;

/// <summary>
/// Единая композиция зависимостей (compile-time DI, Jab). Все компоненты получают
/// зависимости через конструктор.
/// </summary>
[ServiceProvider]
[Singleton(typeof(IDbConnectionFactory), Factory = nameof(CreateConnectionFactory))]
[Singleton(typeof(IClock), typeof(SystemClock))]
[Singleton(typeof(ICadDocumentReader), typeof(JsonCadDocumentReader))]
[Singleton(typeof(ICadSource), typeof(JsonCadSource))]
[Singleton(typeof(ImportPlanner))]
[Singleton(typeof(VersionService))]
[Singleton(typeof(IImportStore), typeof(PostgresImportStore))]
[Singleton(typeof(ImportService))]
[Singleton(typeof(ISpecificationExporter), typeof(CsvSpecificationExporter))]
[Singleton(typeof(IDatabaseInitializer), typeof(PostgresDatabaseInitializer))]
[Singleton(typeof(IPdmQueryRepository), typeof(PdmQueryRepository))]
[Singleton(typeof(IPdmCommandRepository), typeof(PdmCommandRepository))]
[Singleton(typeof(MassCalculator))]
[Singleton(typeof(SpecificationBuilder))]
[Singleton(typeof(IPdmService), typeof(PdmService))]
[Singleton(typeof(MainViewModel))]
[Singleton(typeof(MainWindow))]
public partial class AppContainer
{
    public IDbConnectionFactory CreateConnectionFactory()
        => new NpgsqlConnectionFactory(AppConfiguration.ConnectionString);
}
